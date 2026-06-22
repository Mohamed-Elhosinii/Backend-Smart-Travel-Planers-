using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Interfaces;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using SmartTravelPlaners.DAL.Repositories.Concrete;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartTravelPlaners.BLL.Features.Chat.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepo;
        private readonly ITripRepository _tripRepo;
        private readonly IUserProfileRepository _userProfileRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IChatCompletionService _ai;
        private readonly ITripOrchestratorService _orchestrator;
        private readonly IUsageLimitService _usageLimitService;
        private readonly IServiceProvider _serviceProvider;

        public ChatService(
            IChatRepository chatRepo,
            ITripRepository tripRepo,
            IUserProfileRepository userProfileRepo,
            IUnitOfWork unitOfWork,
            ITripOrchestratorService orchestrator,
            IUsageLimitService usageLimitService,
            IServiceProvider serviceProvider,
            Kernel kernel)
        {
            _chatRepo = chatRepo;
            _tripRepo = tripRepo;
            _userProfileRepo = userProfileRepo;
            _unitOfWork = unitOfWork;
            _ai = kernel.GetRequiredService<IChatCompletionService>();
            _orchestrator = orchestrator;
            _usageLimitService = usageLimitService;
            _serviceProvider = serviceProvider;
        }

        public async Task<ChatReplyDto> SendMessageAsync(Guid sessionId, string userMessage)
        {
            var session = await _chatRepo.GetSessionAsync(sessionId);
            if (session == null)
                throw new Exception("Session not found");

            // ===========================
            // USAGE LIMIT: Check message limit before calling the AI
            // ===========================
            var canSend = await _usageLimitService.CanSendMessageAsync(session.UserId);
            if (!canSend)
            {
                return new ChatReplyDto
                {
                    Message = "You've reached your monthly message limit. Upgrade your plan to continue chatting! 🚀"
                };
            }

            var history = new ChatHistory();

            history.AddSystemMessage(@" You are a smart travel assistant called TravelBot.
Talk to the user in Arabic only, in a friendly and natural way. 
Your job is to collect travel information from the user, and also help them
modify an existing trip plan (hotel, activities, or basic trip fields).

When ALL required fields for a NEW trip are collected, respond ONLY with:

TRIP_READY:{
  ""destination"": """",
  ""originCity"": """",
  ""startDate"": ""yyyy-MM-dd"",
  ""endDate"": ""yyyy-MM-dd"",
  ""numTravelers"": 1,
  ""budgetTotal"": 1000,
  ""preferences"": []
}
If the user asks to see the current trip (e.g. ""ابعت الرحلة"", ""اعرض الرحلة"", ""show my trip""),
respond ONLY with:

TRIP_SHOW:{}

If the user wants to change a simple field of an EXISTING trip (destination, dates,
travelers, budget, originCity), respond ONLY with:

TRIP_UPDATE_FIELD:{""field"": ""destination"", ""value"": ""Paris""}

If the user wants a DIFFERENT HOTEL for their existing trip (e.g. ""غيرلي الفندق"",
""مش عاجبني الفندق ده""), respond ONLY with:

TRIP_UPDATE_HOTEL:{}

If the user wants a DIFFERENT FLIGHT for their existing trip (e.g. ""غيرلي الطيران"",
""عايز رحلة طيران تانية""), respond ONLY with:

TRIP_UPDATE_FLIGHT:{}


If the user wants DIFFERENT ACTIVITIES for a specific day of their existing trip
(e.g. ""عايز أنشطة او اماكن تانية يوم 2""), respond ONLY with:

TRIP_UPDATE_ACTIVITIES:{""dayNumber"": 2}

If the user already has a trip and wants to change ONE field, respond ONLY with:

TRIP_UPDATE:{ ""field"": ""<destination|originCity|startDate|endDate|numTravelers|budgetTotal>"", ""value"": ""<new value>"" }

Rules:
- Always output ONLY one of the formats above when ready, nothing else.
- Do NOT output any other text alongside these formats.
- Destination MUST be in English city name only (e.g., Paris, Dubai, Cairo).
- Dates MUST be in yyyy-MM-dd format.
- If information is missing, continue asking naturally in Arabic.
- Do NOT use multiple formats at once.");

            // Let the model know whether a trip already exists so it picks TRIP_UPDATE vs TRIP_READY.
            if (session.TripId != null)
            {
                history.AddSystemMessage(
                    "The user already has an active trip. If they want to change something, use TRIP_UPDATE (not TRIP_READY).");
            }

            foreach (var msg in session.Messages.OrderBy(m => m.CreatedAt))
            {
                if (msg.Role == MessageRole.User)
                    history.AddUserMessage(msg.Content);
                else
                    history.AddAssistantMessage(msg.Content);
            }

            history.AddUserMessage(userMessage);

            await _chatRepo.AddMessageAsync(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = userMessage,
                CreatedAt = DateTime.UtcNow
            });

            var result = await _ai.GetChatMessageContentAsync(history);
            var rawReply = result.Content ?? "Sorry, unable to respond right now";

            string finalReply;
            TripPlanDto? plan = null;

            // =========================
            // CREATE TRIP FLOW
            // =========================
            if (rawReply.StartsWith("TRIP_READY:") && session.TripId == null)
            {
                // ===========================
                // USAGE LIMIT: Check trip limit before calling external APIs
                // ===========================
                var canTrip = await _usageLimitService.CanGenerateTripAsync(session.UserId);
                if (!canTrip)
                {
                    return new ChatReplyDto
                    {
                        Message = "You've reached your monthly trip generation limit. Upgrade your plan to create more trips! 🚀"
                    };
                }

                var json = rawReply.Substring("TRIP_READY:".Length).Trim();

                var trip = await CreateTripFromJsonAsync(json, session.UserId);

                if (trip == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "حصل مشكلة في إنشاء الرحلة، ممكن نجرب تاني؟"
                    };
                }

                session.TripId = trip.Id;
                session.Stage = ChatStage.PlanReady;

                await _chatRepo.SaveChangesAsync();

                // =========================
                // BACKGROUND ORCHESTRATOR CALL
                // =========================
                var tripId = trip.Id;
                var userId = session.UserId;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                        var usageService = scope.ServiceProvider.GetRequiredService<IUsageLimitService>();
                        
                        await orchestrator.BuildTripPlanAsync(tripId);
                        await usageService.IncrementTripUsageAsync(userId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Orchestrator failed: {ex.Message}");
                    }
                });

                finalReply = $"ممتاز! جاري تجهيز أفضل خطة لرحلتك إلى {trip.Destination} (من {trip.StartDate} إلى {trip.EndDate}). ثواني وهتكون جاهزة ✈️";
                plan = null;

            }

            // =========================
            // View Trip
            // =========================
            else if (rawReply.TrimStart().StartsWith("TRIP_SHOW:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة حالياً.",
                        Plan = null
                    };
                }

                try
                {
                    plan = await _orchestrator.GetCurrentPlanAsync(session.TripId.Value);
                    finalReply = "دي تفاصيل رحلتك ";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetCurrentPlanAsync failed: {ex.Message}");
                    finalReply = "حصلت مشكلة وأنا بجيب الرحلة.";
                }
            }
            // =========================
            // UPDATE HOTEL FLOW
            // =========================
            else if (rawReply.TrimStart().StartsWith("TRIP_UPDATE_HOTEL:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة عشان نغيرلها الفندق. نبدأ نعمل رحلة جديدة؟",
                        Plan = null
                    };
                }

                var tripId = session.TripId.Value;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                        await orchestrator.RegenerateHotelAsync(tripId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"RegenerateHotelAsync failed: {ex.Message}");
                    }
                });

                finalReply = "جاري البحث عن فندق بديل مناسب... ثواني وهنعرضهولك.";
                plan = null;

                session.Stage = ChatStage.Modifying;
            }
            // =========================
            // UPDATE ACTIVITIES FLOW
            // =========================
            else if (rawReply.TrimStart().StartsWith("TRIP_UPDATE_ACTIVITIES:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة عشان نغيرلها الأنشطة. نبدأ نعمل رحلة جديدة؟",
                        Plan = null
                    };
                }

                var json = rawReply.Replace("TRIP_UPDATE_ACTIVITIES:", "").Trim();
                var actOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json, actOptions);
                    var dayNumber = data?.GetValueOrDefault("dayNumber", 0) ?? 0;

                    if (dayNumber <= 0)
                    {
                        finalReply = "ممكن تحدد رقم اليوم اللي عايز تغير أنشطته؟";
                    }
                    else
                    {
                        var tripId = session.TripId.Value;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var scope = _serviceProvider.CreateScope();
                                var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                                await orchestrator.RegenerateDayActivitiesAsync(tripId, dayNumber);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"RegenerateDayActivitiesAsync failed: {ex.Message}");
                            }
                        });

                        finalReply = $"جاري تغيير أنشطة يوم {dayNumber}... ثواني وهنعرضهالك.";
                        plan = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RegenerateDayActivitiesAsync parsing failed: {ex.Message}");
                    finalReply = "حصلت مشكلة وإحنا بنغير الأنشطة، ممكن نجرب تاني؟";
                }

                session.Stage = ChatStage.Modifying;
            }
            // =========================
            // UPDATE FLIGHT FLOW
            // =========================
            else if (rawReply.TrimStart().StartsWith("TRIP_UPDATE_FLIGHT:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة عشان نغيرلها الطيران. نبدأ نعمل رحلة جديدة؟",
                        Plan = null
                    };
                }

                var tripId = session.TripId.Value;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                        await orchestrator.RegenerateFlightAsync(tripId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"RegenerateFlightAsync failed: {ex.Message}");
                    }
                });

                finalReply = "جاري البحث عن رحلة طيران بديلة... ثواني وهنعرضهالك.";
                plan = null;

                session.Stage = ChatStage.Modifying;
            }
            // =========================
            // UPDATE SIMPLE FIELD FLOW
            // =========================
            else if (rawReply.TrimStart().StartsWith("TRIP_UPDATE_FIELD:") ||
                     rawReply.TrimStart().StartsWith("TRIP_UPDATE:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة عشان تتعدل. نبدأ نعمل رحلة جديدة؟",
                        Plan = null
                    };
                }

                var json = rawReply
     .Replace("TRIP_UPDATE_FIELD:", "")
     .Replace("TRIP_UPDATE:", "")
     .Trim();

                await UpdateTripFieldAsync(json, session.TripId);
                await _chatRepo.SaveChangesAsync(); // persist the field change before rebuilding

                session.Stage = ChatStage.Modifying;

                try
                {
                    var tripId = session.TripId.Value;
                    var changedField = await UpdateTripFieldAsync(json, tripId);
                    await _chatRepo.SaveChangesAsync();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                            var tripRepo = scope.ServiceProvider.GetRequiredService<ITripRepository>();

                            switch (changedField?.ToLower())
                            {
                                case "destination":
                                    await orchestrator.RegenerateHotelAsync(tripId);
                                    await orchestrator.RegenerateFlightAsync(tripId);
                                    var tripAfterDest = await tripRepo.GetByIdAsync(tripId);
                                    var daysAfterDest = tripAfterDest != null
                                        ? Math.Max(tripAfterDest.EndDate.DayNumber - tripAfterDest.StartDate.DayNumber, 1) : 1;
                                    for (int day = 1; day <= daysAfterDest; day++)
                                        await orchestrator.RegenerateDayActivitiesAsync(tripId, day);
                                    break;

                                case "startdate":
                                case "enddate":
                                    await orchestrator.RegenerateHotelAsync(tripId);
                                    await orchestrator.RegenerateFlightAsync(tripId);
                                    await orchestrator.SyncDayPlansAsync(tripId);
                                    break;

                                case "numtravelers":
                                    await orchestrator.RegenerateHotelAsync(tripId);
                                    break;

                                case "budgettotal":
                                    await orchestrator.RegenerateHotelAsync(tripId);
                                    var tripAfterBudget = await tripRepo.GetByIdAsync(tripId);
                                    var daysAfterBudget = tripAfterBudget != null
                                        ? Math.Max(tripAfterBudget.EndDate.DayNumber - tripAfterBudget.StartDate.DayNumber, 1) : 1;
                                    for (int day = 1; day <= daysAfterBudget; day++)
                                        await orchestrator.RegenerateDayActivitiesAsync(tripId, day);
                                    break;

                                case "origincity":
                                    await orchestrator.RegenerateFlightAsync(tripId);
                                    break;
                                
                                default:
                                    // Just rebuild plan if field is something else
                                    await orchestrator.BuildTripPlanAsync(tripId);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Field cascade update failed: {ex.Message}");
                        }
                    });

                    finalReply = "تم استلام التعديلات وجاري تحديث تفاصيل الرحلة... ثواني وتكون جاهزة.";
                    plan = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Update field failed: {ex.Message}");
                    finalReply = "حصلت مشكلة في تحديث البيانات، ممكن تجرب تاني.";
                }
            }
           
            else
                {
                    finalReply = rawReply;
                }
            

            await _chatRepo.AddMessageAsync(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.Assistant,
                Content = finalReply,
                CreatedAt = DateTime.UtcNow
            });

            // Increment message usage after successful AI reply
            await _usageLimitService.IncrementMessageUsageAsync(session.UserId);

            session.UpdatedAt = DateTime.UtcNow;
            await _chatRepo.SaveChangesAsync();

            return new ChatReplyDto
            {
                Message = finalReply,
                Plan = plan
            };
        }

        // =========================
        // CREATE TRIP (unchanged)
        // =========================
        private async Task<Trip> CreateTripFromJsonAsync(string json, string userId)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var data = JsonSerializer.Deserialize<TripCreateDto>(json, options)
                          ?? throw new Exception("Failed to parse trip data from AI");

            var profiles = await _userProfileRepo.FindAsync(p => p.AspNetUserId == userId);

            var userProfile = profiles.FirstOrDefault()
                              ?? throw new Exception("User profile not found");

            if (!DateOnly.TryParse(data.StartDate, out var startDate))
                throw new Exception("Invalid start date");

            if (!DateOnly.TryParse(data.EndDate, out var endDate))
                throw new Exception("Invalid end date");

            if (!int.TryParse(data.NumTravelers.ToString(), out var travelers))
                throw new Exception("Invalid travelers count");

            if (!decimal.TryParse(data.BudgetTotal.ToString(), out var budget))
                throw new Exception("Invalid budget");

            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                UserId = userProfile.Id,
                Title = $"Trip to {data.Destination}",
                Destination = data.Destination,
                OriginCity = data.OriginCity,
                StartDate = startDate,
                EndDate = endDate,
                NumTravelers = travelers,
                BudgetTotal = budget,
                Status = TripStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                Preferences = data.Preferences.Select(p => new TripPreference
                {
                    Id = Guid.NewGuid(),
                    Category = "General",
                    Value = p
                }).ToList()
            };

            await _unitOfWork.Trips.AddAsync(trip);
            await _unitOfWork.CompleteAsync();

            return trip;
        }

        // =========================
        // UPDATE TRIP 
        // =========================
        private async Task<string?> UpdateTripFieldAsync(string json, Guid? tripId)
        {
            if (tripId == null) return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var update = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);
            if (update == null) return null;

            var trip = await _tripRepo.GetByIdAsync(tripId.Value);
            if (trip == null) return null;

            var field = update.GetValueOrDefault("field", "");
            var value = update.GetValueOrDefault("value", "");

            switch (field.ToLower())
            {
                case "destination":
                    trip.Destination = value;
                    trip.Title = $"Trip to {value}";
                    break;
                case "startdate":
                    var newStart = DateOnly.Parse(value);
                    if (newStart >= trip.EndDate)
                        throw new Exception("تاريخ البداية لازم يكون قبل تاريخ النهاية");
                    trip.StartDate = newStart;
                    break;
                case "enddate":
                    var newEnd = DateOnly.Parse(value);
                    if (newEnd <= trip.StartDate)
                        throw new Exception("تاريخ النهاية لازم يكون بعد تاريخ البداية");
                    trip.EndDate = newEnd;
                    break;
                case "numtravelers":
                    trip.NumTravelers = int.Parse(value);
                    break;
                case "budgettotal":
                    trip.BudgetTotal = decimal.Parse(value);
                    break;
                case "origincity":
                    trip.OriginCity = value;
                    break;
            }

            _tripRepo.Update(trip);

            return field.ToLower(); 
        }

        public async Task<ChatSession> CreateSessionAsync(string userId)
        {
            var session = await _chatRepo.CreateSessionAsync(userId);
            await _chatRepo.SaveChangesAsync();
            return session;
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId)
        {
            return await _chatRepo.GetMessagesAsync(sessionId);
        }

        public async Task<TripPlanDto?> GetTripPlanAsync(Guid tripId)
        {
            try
            {
                return await _orchestrator.GetCurrentPlanAsync(tripId);
            }
            catch
            {
                return null;
            }
        }
    }
}