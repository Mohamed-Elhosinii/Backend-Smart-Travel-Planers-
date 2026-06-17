using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Interfaces;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
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

        public ChatService(
            IChatRepository chatRepo,
            ITripRepository tripRepo,
            IUserProfileRepository userProfileRepo,
            IUnitOfWork unitOfWork,
            ITripOrchestratorService orchestrator,
            Kernel kernel)
        {
            _chatRepo = chatRepo;
            _tripRepo = tripRepo;
            _userProfileRepo = userProfileRepo;
            _unitOfWork = unitOfWork;
            _ai = kernel.GetRequiredService<IChatCompletionService>();
            _orchestrator = orchestrator;
        }

        public async Task<ChatReplyDto> SendMessageAsync(Guid sessionId, string userMessage)
        {
            var session = await _chatRepo.GetSessionAsync(sessionId);
            if (session == null)
                throw new Exception("Session not found");

            var history = new ChatHistory();

            history.AddSystemMessage(@" You are a smart travel assistant called TravelBot.
Talk to the user in Arabic only, in a friendly and natural way. 
Your job:
1. Collect all of the following from the user naturally through conversation: 
- Destination (destination) 
- Travel and return dates (startDate, endDate) in yyyy-MM-dd format
- Number of travelers (numTravelers) - Total budget in USD (budgetTotal) 
- Departure city (originCity)
- Interests e.g. nature, history, food (preferences)

2. Once you have ALL the information, reply with ONLY this line and nothing else:
TRIP_READY:{""destination"":""..."",""originCity"":""..."",""startDate"":""..."",""endDate"":""..."",""numTravelers"":1,""budgetTotal"":1000,""preferences"":[""...""]}

3. If the user wants to change something after the trip is created, reply with ONLY: 
TRIP_UPDATE:{""field"":""..."",""value"":""...""}. 
ONLY use TRIP_UPDATE if TripId already exists in system context.
If no trip exists, ALWAYS use TRIP_READY. 
Never include any extra text when sending TRIP_READY or TRIP_UPDATE. 
Always ask about missing info naturally before sending TRIP_READY. 
Once you have all required information, respond ONLY with valid JSON in this exact format:
{ 
""destination"": """",
""originCity"": """", 
""startDate"": ""yyyy-MM-dd"", 
""endDate"": ""yyyy-MM-dd"", 
""numTravelers"": 1, 
""budgetTotal"": 1000,
""preferences"": [] 
} 
Do NOT add any text before or after the JSON. ");

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
            if (rawReply.StartsWith("TRIP_READY:"))
            {
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

                session.Stage = ChatStage.Modifying;

                finalReply = "تم تحديث الرحلة بنجاح!";
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

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId)
        {
            return await _chatRepo.GetMessagesAsync(sessionId);
        }
    }
}