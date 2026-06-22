using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public ChatService(
            IChatRepository chatRepo,
            ITripRepository tripRepo,
            IUserProfileRepository userProfileRepo,
            IUnitOfWork unitOfWork,
            ITripOrchestratorService orchestrator,
            IUsageLimitService usageLimitService,
            Kernel kernel,
            IConfiguration configuration)
        {
            _chatRepo = chatRepo;
            _tripRepo = tripRepo;
            _userProfileRepo = userProfileRepo;
            _unitOfWork = unitOfWork;
            _ai = kernel.GetRequiredService<IChatCompletionService>();
            _orchestrator = orchestrator;
            _usageLimitService = usageLimitService;
            _configuration = configuration;
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

            history.AddSystemMessage(@" You are a smart travel assistant called TravelBot.
CRITICAL: Always reply in the exact same language used by the user. If the user speaks or asks in English, your response must be in English. If the user speaks or asks in Arabic, your response must be in Arabic.
Talk to the user in a friendly and natural way.
Your job is to collect travel information from the user.

When ALL required fields are collected, respond ONLY with:

TRIP_READY:{
  ""destination"": """",
  ""originCity"": """",
  ""startDate"": ""yyyy-MM-dd"",
  ""endDate"": ""yyyy-MM-dd"",
  ""numTravelers"": 1,
  ""budgetTotal"": 1000,
  ""preferences"": []
}

If the user already has a trip and wants to change ONE field, respond ONLY with:

TRIP_UPDATE:{ ""field"": ""<destination|originCity|startDate|endDate|numTravelers|budgetTotal>"", ""value"": ""<new value>"" }

Rules:
- When ready to create a trip, output ONLY the TRIP_READY format.
- When updating an existing trip, output ONLY the TRIP_UPDATE format.
- Do NOT output any other text alongside these formats.
- Destination MUST be in English city name only (e.g., Paris, Dubai, Cairo).
- Dates MUST be in yyyy-MM-dd format.
- If information is missing, continue asking naturally in the user's language.
- Do NOT use multiple formats at once. ");

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
            // CREATE TRIP FLOW
            // =========================
            if (rawReply.StartsWith("TRIP_READY:"))
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
                // SAFE ORCHESTRATOR CALL
                // =========================
                try
                {
                    plan = await _orchestrator.BuildTripPlanAsync(trip.Id);

                    // Increment trip usage after successful plan build
                    await _usageLimitService.IncrementTripUsageAsync(session.UserId);
                }
                catch (Exception ex)
                {
                    plan = null;
                    Console.WriteLine($"Orchestrator failed: {ex.Message}");
                }

                finalReply = $"تم إنشاء رحلتك إلى {trip.Destination}! " +
                             $"من {trip.StartDate} إلى {trip.EndDate}. ";

                if (plan != null)
                    finalReply += plan.Summary;
                else
                    finalReply += "لكن بعض تفاصيل الرحلة (مثل الطيران) غير متاحة حاليًا.";

            }
            else if (rawReply.TrimStart().StartsWith("TRIP_UPDATE:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة عشان تتعدل. نبدأ نعمل رحلة جديدة؟",
                        Plan = null
                    };
                }

                var json = rawReply.Replace("TRIP_UPDATE:", "").Trim();

                await UpdateTripFieldAsync(json, session.TripId);
                await _chatRepo.SaveChangesAsync(); // persist the field change before rebuilding

                session.Stage = ChatStage.Modifying;

                // Rebuild the plan so the change (dates / budget / destination / ...) is reflected.
                try
                {
                    plan = await _orchestrator.BuildTripPlanAsync(session.TripId.Value);
                }
                catch (Exception ex)
                {
                    plan = null;
                    Console.WriteLine($"Orchestrator rebuild failed: {ex.Message}");
                }

                finalReply = plan != null
                    ? "تم تحديث الرحلة وإعادة بناء الخطة بنجاح! " + plan.Summary
                    : "تم تحديث الرحلة، لكن حصل مشكلة في إعادة بناء بعض تفاصيل الخطة.";
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
        // UPDATE TRIP (unchanged)
        // =========================
        private async Task UpdateTripFieldAsync(string json, Guid? tripId)
        {
            if (tripId == null) return;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var update = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);
            if (update == null) return;

            var trip = await _tripRepo.GetByIdAsync(tripId.Value);
            if (trip == null) return;

            var field = update.GetValueOrDefault("field", "");
            var value = update.GetValueOrDefault("value", "");

            switch (field.ToLower())
            {
                case "destination":
                    trip.Destination = value;
                    trip.Title = $"Trip to {value}";
                    break;
                case "startdate":
                    trip.StartDate = DateOnly.Parse(value);
                    break;
                case "enddate":
                    trip.EndDate = DateOnly.Parse(value);
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
        }

        public async Task<ChatSession> CreateSessionAsync(string userId)
        {
            var session = await _chatRepo.CreateSessionAsync(userId);
            await _chatRepo.SaveChangesAsync();
            return session;
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId, string userId)
        {
            var session = await _chatRepo.GetSessionAsync(sessionId);
            if (session == null || session.UserId != userId)
                throw new UnauthorizedAccessException("You do not have access to this session.");

            return await _chatRepo.GetMessagesAsync(sessionId);
        }

        public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
        {
            return await _chatRepo.GetSessionsByUserAsync(userId);
        }
    }
}