using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Interfaces;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Text.Json;

namespace SmartTravelPlaners.BLL.Features.Chat.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepo;
        private readonly ITripRepository _tripRepo;
        private readonly IUserProfileRepository _userProfileRepo;
        private readonly IChatCompletionService _ai;
        private readonly ITripOrchestratorService _orchestrator;
        private readonly IUsageLimitService _usageLimitService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ITripCreationService _tripCreationService;

        public ChatService(
            IChatRepository chatRepo,
            ITripRepository tripRepo,
            IUserProfileRepository userProfileRepo,
            ITripOrchestratorService orchestrator,
            IUsageLimitService usageLimitService,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ITripCreationService tripCreationService,
            Kernel kernel)
        {
            _chatRepo = chatRepo;
            _tripRepo = tripRepo;
            _userProfileRepo = userProfileRepo;
            _ai = kernel.GetRequiredService<IChatCompletionService>();
            _orchestrator = orchestrator;
            _usageLimitService = usageLimitService;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _tripCreationService = tripCreationService;
        }

        public async Task<ChatReplyDto> SendMessageAsync(Guid sessionId, string userId, string userMessage)
        {
            var session = await _chatRepo.GetSessionAsync(sessionId);
            if (session == null)
                throw new Exception("Session not found");

            if (session.UserId != userId)
                throw new UnauthorizedAccessException("You do not have access to this session.");

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

            var apiKey = _configuration["GitHubModels:Token"];
            var endpoint = _configuration["GitHubModels:Endpoint"];
            var modelId = _configuration["GitHubModels:ModelId"];

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException("GitHubModels:Token", "API Key is missing or empty in appsettings.json.");

            Console.WriteLine("DEBUG BACKEND: Received message from user, initiating LLM call...");
            Console.WriteLine($"DEBUG BACKEND: Hitting provider Endpoint: {endpoint} | ModelId: {modelId}");

            Microsoft.SemanticKernel.ChatMessageContent result = null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                result = await _ai.GetChatMessageContentAsync(history, null, null, cts.Token);
                Console.WriteLine("DEBUG BACKEND: LLM call completed successfully.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("DEBUG BACKEND ERROR: LLM call timed out after 15 seconds.");
                return new ChatReplyDto { Message = "Sorry, the AI service took too long to respond. Please try again later." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG BACKEND ERROR: LLM call failed or timed out: {ex.Message}");
                return new ChatReplyDto { Message = "Sorry, the AI service is currently unavailable. Please try again later." };
            }

            var rawReply = result?.Content ?? "Sorry, unable to respond right now";

            string finalReply;
            TripPlanDto? plan = null;

            static string? ExtractAfter(string input, string keyword)
            {
                var idx = input.IndexOf(keyword, StringComparison.Ordinal);
                if (idx < 0) return null;
                return input.Substring(idx + keyword.Length).Trim();
            }

            bool HasKeyword(string keyword) =>
                rawReply.Contains(keyword, StringComparison.Ordinal);

            // CREATE TRIP
            if (HasKeyword("TRIP_READY:") && session.TripId == null)
            {
                var json = ExtractAfter(rawReply, "TRIP_READY:")!;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dto = JsonSerializer.Deserialize<TripCreateDto>(json, options)
                              ?? throw new Exception("Failed to parse trip data from AI");

                var creationResult = await _tripCreationService.CreateAndBuildAsync(dto, session.UserId);
                if (creationResult.LimitReached)
                    return new ChatReplyDto { Message = creationResult.Message! };

                var trip = creationResult.Trip!;
                session.TripId = trip.Id;
                session.Stage = ChatStage.PlanReady;
                await _chatRepo.SaveChangesAsync();

                finalReply = $"ممتاز! جاري تجهيز أفضل خطة لرحلتك إلى {trip.Destination} (من {trip.StartDate} إلى {trip.EndDate}). ثواني وهتكون جاهزة ✈️";
                plan = null;
            }
            // VIEW TRIP
            else if (HasKeyword("TRIP_SHOW:"))
            {
                if (session.TripId == null)
                    return new ChatReplyDto { Message = "مفيش رحلة موجودة حالياً.", Plan = null };

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
            // UPDATE HOTEL
            else if (HasKeyword("TRIP_UPDATE_HOTEL:"))
            {
                if (session.TripId == null)
                    return new ChatReplyDto { Message = "مفيش رحلة موجودة عشان نغيرلها الفندق. نبدأ نعمل رحلة جديدة؟", Plan = null };

                var tripId = session.TripId.Value;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                        await orchestrator.RegenerateHotelAsync(tripId);
                    }
                    catch (Exception ex) { Console.WriteLine($"RegenerateHotelAsync failed: {ex.Message}"); }
                });

                finalReply = "جاري البحث عن فندق بديل مناسب... ثواني وهنعرضهولك.";
                plan = null;
                session.Stage = ChatStage.Modifying;
            }
            // UPDATE ACTIVITIES
            else if (HasKeyword("TRIP_UPDATE_ACTIVITIES:"))
            {
                if (session.TripId == null)
                    return new ChatReplyDto { Message = "مفيش رحلة موجودة عشان نغيرلها الأنشطة. نبدأ نعمل رحلة جديدة؟", Plan = null };

                var json = ExtractAfter(rawReply, "TRIP_UPDATE_ACTIVITIES:")!;
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
                            catch (Exception ex) { Console.WriteLine($"RegenerateDayActivitiesAsync failed: {ex.Message}"); }
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
            // UPDATE FLIGHT
            else if (HasKeyword("TRIP_UPDATE_FLIGHT:"))
            {
                if (session.TripId == null)
                    return new ChatReplyDto { Message = "مفيش رحلة موجودة عشان نغيرلها الطيران. نبدأ نعمل رحلة جديدة؟", Plan = null };

                var tripId = session.TripId.Value;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                        await orchestrator.RegenerateFlightAsync(tripId);
                    }
                    catch (Exception ex) { Console.WriteLine($"RegenerateFlightAsync failed: {ex.Message}"); }
                });

                finalReply = "جاري البحث عن رحلة طيران بديلة... ثواني وهنعرضهالك.";
                plan = null;
                session.Stage = ChatStage.Modifying;
            }
            // UPDATE SIMPLE FIELD
            else if (HasKeyword("TRIP_UPDATE_FIELD:") || HasKeyword("TRIP_UPDATE:"))
            {
                if (session.TripId == null)
                    return new ChatReplyDto { Message = "مفيش رحلة موجودة عشان تتعدل. نبدأ نعمل رحلة جديدة؟", Plan = null };

                var json = (ExtractAfter(rawReply, "TRIP_UPDATE_FIELD:")
                         ?? ExtractAfter(rawReply, "TRIP_UPDATE:"))!;

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
                                    await orchestrator.BuildTripPlanAsync(tripId);
                                    break;
                            }
                        }
                        catch (Exception ex) { Console.WriteLine($"Field cascade update failed: {ex.Message}"); }
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

            await _usageLimitService.IncrementMessageUsageAsync(session.UserId);

            session.UpdatedAt = DateTime.UtcNow;
            await _chatRepo.SaveChangesAsync();

            return new ChatReplyDto
            {
                Message = finalReply,
                Plan = plan,
                TripId = session.TripId
            };
        }

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

        public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
        {
            return await _chatRepo.GetSessionsByUserAsync(userId);
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId, string userId)
        {
            var session = await _chatRepo.GetSessionAsync(sessionId);
            if (session == null || session.UserId != userId)
                throw new UnauthorizedAccessException("You do not have access to this session.");

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

        public async Task LinkSessionToTripAsync(Guid sessionId, string userId, Guid tripId)
        {
            var session = await _chatRepo.GetSessionAsync(sessionId);
            if (session == null)
                throw new Exception("Session not found");

            if (session.UserId != userId)
                throw new UnauthorizedAccessException("You do not have access to this session.");

            session.TripId = tripId;
            session.Stage = ChatStage.PlanReady;
            session.UpdatedAt = DateTime.UtcNow;

            await _chatRepo.SaveChangesAsync();
        }
    }
}