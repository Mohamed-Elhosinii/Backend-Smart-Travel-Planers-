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
        private readonly IConfiguration _configuration;
        private readonly ITripCreationService _tripCreationService;

        public ChatService(
            IChatRepository chatRepo,
            ITripRepository tripRepo,
            IUserProfileRepository userProfileRepo,
            IUnitOfWork unitOfWork,
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
            _unitOfWork = unitOfWork;
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

            history.AddSystemMessage(@"You are a smart travel assistant called TravelBot.
Talk to the user in the SAME LANGUAGE they use to talk to you, in a friendly and natural way.
You must be highly intelligent and flexible in understanding the user's input. They do not have to provide information in a strict format.
Your job is to collect travel information to plan a new trip, or help them modify an existing trip plan.

If the user is planning a new journey and they mention their destination and origin at any point, even if you are still collecting other info, you MUST output:
TRIP_TITLE:{ ""title"": ""[Title based on user language, e.g. رحلة من القاهرة إلى باريس]"" }

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

If the user asks to see the current trip (e.g. ""ابعت الرحلة"", ""اعرض الرحلة"", ""show my trip""), respond ONLY with:
TRIP_SHOW:{}

If the user wants to change a simple field of an EXISTING trip (destination, dates, travelers, budget, originCity), respond ONLY with:
TRIP_UPDATE_FIELD:{""field"": ""destination"", ""value"": ""Paris""}

If the user wants a DIFFERENT HOTEL for their existing trip, respond ONLY with:
TRIP_UPDATE_HOTEL:{}

If the user wants a DIFFERENT FLIGHT for their existing trip, respond ONLY with:
TRIP_UPDATE_FLIGHT:{}

If the user wants DIFFERENT ACTIVITIES for a specific day of their existing trip, respond ONLY with:
TRIP_UPDATE_ACTIVITIES:{""dayNumber"": 2}

Rules:
- Always output ONLY one of the formats above when ready, nothing else.
- Do NOT output any other text alongside these formats.
- Destination MUST be in English city name only (e.g., Paris, Dubai, Cairo).
- Dates MUST be in yyyy-MM-dd format.
- If information is missing, continue asking naturally. Do NOT force the user to answer in a strict way.
- You can understand conversational context intelligently.");

            // Let the model know whether a trip already exists so it picks TRIP_UPDATE vs TRIP_READY.

            if (session.TripId != null)
            {
                var trip = await _tripRepo.GetByIdAsync(session.TripId.Value);
                history.AddSystemMessage($@"
The user already has an active trip. If they want to change something, use TRIP_UPDATE_FIELD (not TRIP_READY).

Current trip details:
Destination: {trip.Destination}
Origin: {trip.OriginCity}
Start Date: {trip.StartDate}
End Date: {trip.EndDate}
Travelers: {trip.NumTravelers}
Budget: {trip.BudgetTotal}

IMPORTANT RULES:
- The user is referring to THIS trip ONLY.
- Never ask for the destination or any trip details again.
- Never ask for confirmation before making changes.
-  When the user says they want to change something but does NOT provide the new value, ask for it first in Arabic.
- Only respond with the format AFTER you have the new value.
- For example: If the user says 'عدل الدولة' or 'غير الدولة' without specifying, ask: 'هل تقصد دولة الوجهة أم مدينة الانطلاق؟'
- If they say 'الوجهة' or 'الذهاب', use TRIP_UPDATE_FIELD with field 'destination'.
- If they say 'الانطلاق' or 'المغادرة', use TRIP_UPDATE_FIELD with field 'originCity'.
- Do NOT say 'هل أنت متأكد' or 'هل تريد تغيير' — just do it directly.
- If user says 'غير الأنشطة' or 'اقترح أماكن', respond ONLY with TRIP_UPDATE_ACTIVITIES and the day number.
- If the user wants to change dates WITHOUT specifying which date (start or end), ask them first: 'هل تريد تغيير تاريخ البداية أم تاريخ العودة؟'
- If the user specifies the date type AND the new value, respond IMMEDIATELY with TRIP_UPDATE_FIELD.
- If user says 'تمام' or 'موافق' after a change, just confirm the change was made.
");
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
            {
                throw new ArgumentNullException("GitHubModels:Token", "API Key is missing or empty in appsettings.json.");
            }

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

            // =========================
            // HELPER: extract JSON after keyword (handles text before keyword)
            // =========================
            static string? ExtractAfter(string input, string keyword)
            {
                var idx = input.IndexOf(keyword, StringComparison.Ordinal);
                if (idx < 0) return null;
                return input.Substring(idx + keyword.Length).Trim();
            }

            bool HasKeyword(string keyword) =>
                rawReply.Contains(keyword, StringComparison.Ordinal);

            // Intercept TRIP_TITLE if present
            if (HasKeyword("TRIP_TITLE:"))
            {
                var titleJson = ExtractAfter(rawReply, "TRIP_TITLE:");
                if (titleJson != null)
                {
                    try
                    {
                        // Clean up to just the json block if there's text after
                        var match = Regex.Match(titleJson, @"\{.*?\}", RegexOptions.Singleline);
                        if (match.Success)
                        {
                            var titleObj = JsonSerializer.Deserialize<Dictionary<string, string>>(match.Value);
                            if (titleObj != null && titleObj.TryGetValue("title", out var titleValue))
                            {
                                session.Title = titleValue;
                                _unitOfWork.CompleteAsync().Wait(); // Save title immediately, we'll await later or just let the final save pick it up. Actually, better to just set it and let it save at the end.
                            }
                            // Strip out the TRIP_TITLE command from rawReply so the user doesn't see it if there's conversational text.
                            rawReply = rawReply.Replace("TRIP_TITLE:" + match.Value, "").Trim();
                        }
                    }
                    catch { /* ignore parse errors for title */ }
                }
            }

            // =========================
            // CREATE TRIP FLOW
            // =========================
            if (HasKeyword("TRIP_READY:") && session.TripId == null)
            {
                var json = ExtractAfter(rawReply, "TRIP_READY:")!;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dto = JsonSerializer.Deserialize<TripCreateDto>(json, options)
                              ?? throw new Exception("Failed to parse trip data from AI");
                if (DateOnly.TryParse(dto.StartDate, out var startDate) && startDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    finalReply = "عذراً، لا يمكن إنشاء رحلة بتاريخ في الماضي. من فضلك اختر تاريخ مستقبلي.";
                    plan = null;
                }

                // Shared create → background-build → usage pipeline
                // (the SAME path as POST /api/Trip/quick-plan; one limit check, one
                //  persistence path, one BuildTripPlanAsync, one usage increment).
                else
                {
                    var creationResult = await _tripCreationService.CreateAndBuildAsync(dto, session.UserId);

                    if (creationResult.LimitReached)
                    {
                        return new ChatReplyDto { Message = creationResult.Message! };
                    }

                    var trip = creationResult.Trip!;
                    session.TripId = trip.Id;
                    session.Stage = ChatStage.PlanReady;

                    await _chatRepo.SaveChangesAsync();

                    finalReply = $"ممتاز! جاري تجهيز أفضل خطة لرحلتك إلى {trip.Destination} (من {trip.StartDate} إلى {trip.EndDate}). ثواني وهتكون جاهزة ✈️";
                    plan = null;
                }
            }

            // =========================
            // VIEW TRIP FLOW
            // =========================
            else if (HasKeyword("TRIP_SHOW:"))
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
            else if (HasKeyword("TRIP_UPDATE_HOTEL:"))
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
            else if (HasKeyword("TRIP_UPDATE_ACTIVITIES:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة عشان نغيرلها الأنشطة. نبدأ نعمل رحلة جديدة؟",
                        Plan = null
                    };
                }

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
            else if (HasKeyword("TRIP_UPDATE_FLIGHT:"))
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
            else if (HasKeyword("TRIP_UPDATE_FIELD:") || HasKeyword("TRIP_UPDATE:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة عشان تتعدل. نبدأ نعمل رحلة جديدة؟",
                        Plan = null
                    };
                }

                var json = (ExtractAfter(rawReply, "TRIP_UPDATE_FIELD:")
                         ?? ExtractAfter(rawReply, "TRIP_UPDATE:"))!;

                try
                {
                    var tripId = session.TripId.Value;
                    var changedField = await UpdateTripFieldAsync(json, tripId); // ← مرة واحدة بس
                    await _chatRepo.SaveChangesAsync();

                    session.Stage = ChatStage.Modifying;

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
                                    await Task.Delay(500); // ← استني شوية
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
                                    await orchestrator.RegenerateWeatherAsync(tripId);
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
                    finalReply = ex.Message;
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
                Plan = plan,
                TripId = session.TripId
            };
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
                    if (newStart < DateOnly.FromDateTime(DateTime.Today))
                        throw new Exception("لا يمكن تغيير تاريخ البداية لتاريخ في الماضي");
                    if (newStart >= trip.EndDate)
                        throw new Exception("تاريخ البداية لازم يكون قبل تاريخ النهاية");
                    trip.StartDate = newStart;
                    break;

                case "enddate":
                    var newEnd = DateOnly.Parse(value);
                    if (newEnd < DateOnly.FromDateTime(DateTime.Today))
                        throw new Exception("لا يمكن تغيير تاريخ النهاية لتاريخ في الماضي");
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
            await _unitOfWork.CompleteAsync(); 
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

        public async Task<TripPlanDto?> GetTripPlanAsync(Guid tripId, string userId)
        {
            try
            {
                var profile = await _userProfileRepo.GetUserProfileWithPreferencesAsync(userId);
                if (profile == null) return null;

                var trip = await _tripRepo.GetByIdAsync(tripId);
                if (trip == null || trip.UserId != profile.Id)
                    return null;

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

        public async Task<ChatSession> GetOrCreateTripSessionAsync(Guid tripId, string userId)
        {
            // لو فيه Session موجودة للرحلة، رجعها
            var existingSession = await _chatRepo.GetSessionByTripIdAsync(tripId, userId);

            if (existingSession != null)
                return existingSession;

            // لو مفيش، اعمل Session جديدة
            var session = await _chatRepo.CreateSessionAsync(userId);

            session.TripId = tripId;
            session.Stage = ChatStage.PlanReady;

            await _chatRepo.SaveChangesAsync();

            return session;
        }
    }
}