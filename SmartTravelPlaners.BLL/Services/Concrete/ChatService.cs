using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartTravelPlaners.BLL.DTOs.Chat;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Text.Json;

namespace SmartTravelPlaners.BLL.Services.Concrete
{
    public class ChatService
    {
        private readonly IChatRepository _chatRepo;
        private readonly ITripRepository _tripRepo;
        private readonly IUserProfileRepository _userProfileRepo;
        private readonly IChatCompletionService _ai;

        public ChatService(
            IChatRepository chatRepo,
            ITripRepository tripRepo,
            IUserProfileRepository userProfileRepo,
            Kernel kernel)
        {
            _chatRepo = chatRepo;
            _tripRepo = tripRepo;
            _userProfileRepo = userProfileRepo;
            _ai = kernel.GetRequiredService<IChatCompletionService>();
        }

        public async Task<string> SendMessageAsync(Guid sessionId, string userMessage)
        {
            var session = await _chatRepo.GetSessionAsync(sessionId);
            if (session == null)
                throw new Exception("Session not found");

            // build chat history from saved DB messages
            var history = new ChatHistory();
            history.AddSystemMessage(@"
                You are a smart travel assistant called TravelBot.
                Talk to the user in Arabic only, in a friendly and natural way.

                Your job:
                1. Collect all of the following from the user naturally through conversation:
                   - Destination (destination)
                   - Travel and return dates (startDate, endDate) in yyyy-MM-dd format
                   - Number of travelers (numTravelers)
                   - Total budget in USD (budgetTotal)
                   - Departure city (originCity)
                   - Interests e.g. nature, history, food (preferences)

                2. Once you have ALL the information, reply with ONLY this line and nothing else:
                TRIP_READY:{""destination"":""..."",""originCity"":""..."",""startDate"":""..."",""endDate"":""..."",""numTravelers"":1,""budgetTotal"":1000,""preferences"":[""...""]}

                3. If the user wants to change something after the trip is created, reply with ONLY:
                TRIP_UPDATE:{""field"":""..."",""value"":""...""}

                Never include any extra text when sending TRIP_READY or TRIP_UPDATE.
                Always ask about missing info naturally before sending TRIP_READY.
            ");

            foreach (var msg in session.Messages.OrderBy(m => m.CreatedAt))
            {
                if (msg.Role == MessageRole.User)
                    history.AddUserMessage(msg.Content);
                else
                    history.AddAssistantMessage(msg.Content);
            }

            history.AddUserMessage(userMessage);

            // save user message first
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

            if (rawReply.TrimStart().StartsWith("TRIP_READY:"))
            {
                var json = rawReply.Trim().Replace("TRIP_READY:", "").Trim();
                var trip = await CreateTripFromJsonAsync(json, session.UserId);

                // link this session to the newly created trip
                session.TripId = trip.Id;
                session.Stage = ChatStage.PlanReady;

                finalReply = $"Trip to {trip.Destination} has been created successfully! " +
                             $"From {trip.StartDate} to {trip.EndDate}. " +
                             $"Budget: {trip.BudgetTotal}$. " +
                             "You can modify anything you want!";
            }
            else if (rawReply.TrimStart().StartsWith("TRIP_UPDATE:"))
            {
                var json = rawReply.Trim().Replace("TRIP_UPDATE:", "").Trim();
                await UpdateTripFieldAsync(json, session.TripId);

                session.Stage = ChatStage.Modifying;
                finalReply = "Trip updated successfully!";
            }
            else
            {
                finalReply = rawReply;
            }

            // save AI reply
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

            return finalReply;
        }

        public async Task<ChatSession> CreateSessionAsync(string userId)
        {
            // return existing session if user already has one
            var existing = await _chatRepo.GetSessionByUserAsync(userId);
            if (existing != null)
                return existing;

            var session = await _chatRepo.CreateSessionAsync(userId);
            await _chatRepo.SaveChangesAsync();
            return session;
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId)
        {
            return await _chatRepo.GetMessagesAsync(sessionId);
        }

        // -------------------------------------------------------
        // Private Helpers
        // -------------------------------------------------------

        private async Task<Trip> CreateTripFromJsonAsync(string json, string userId)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<TripCreateDto>(json, options)
                       ?? throw new Exception("Failed to parse trip data from AI");

            // find the UserProfile linked to this AspNetUser id
            var profiles = await _userProfileRepo.FindAsync(p => p.AspNetUserId == userId);
            var userProfile = profiles.FirstOrDefault()
                              ?? throw new Exception("User profile not found");

            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                UserId = userProfile.Id,
                Title = $"Trip to {data.Destination}",
                Destination = data.Destination,
                OriginCity = data.OriginCity,
                StartDate = DateOnly.Parse(data.StartDate),
                EndDate = DateOnly.Parse(data.EndDate),
                NumTravelers = data.NumTravelers,
                BudgetTotal = data.BudgetTotal,
                Status = TripStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                Preferences = data.Preferences.Select(p => new TripPreference
                {
                    Id = Guid.NewGuid(),
                    Category = "General",
                    Value = p
                }).ToList()
            };

            await _tripRepo.AddAsync(trip);
            return trip;
        }

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

            // apply the requested field change
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

            // Update() is sync in GenericRepository - no await needed
            _tripRepo.Update(trip);
        }
    }
}