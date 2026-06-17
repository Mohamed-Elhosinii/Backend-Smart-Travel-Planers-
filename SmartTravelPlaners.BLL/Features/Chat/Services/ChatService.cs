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
                2.Always convert all cities and locations into IATA airport codes.

                  Examples:
                  - Cairo → CAI
                  - Paris → CDG
                  - London → LHR
                 Return ONLY standardized codes, never city names.
                 If you cannot find IATA code, make your best guess based on major international airports.
                 Never return Arabic or city names.

                3. Once you have ALL the information, reply with ONLY this line and nothing else:
                TRIP_READY:{""destination"":""..."",""originCity"":""..."",""startDate"":""..."",""endDate"":""..."",""numTravelers"":1,""budgetTotal"":1000,""preferences"":[""...""]}

                4. If the user wants to change something after the trip is created, reply with ONLY:
                TRIP_UPDATE:{""field"":""..."",""value"":""...""}.
                ONLY use TRIP_UPDATE if TripId already exists in system context.If no trip exists, ALWAYS use TRIP_READY.
                

                

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

            if (rawReply.TrimStart().StartsWith("TRIP_READY:"))
            {
                var json = rawReply.Trim().Replace("TRIP_READY:", "").Trim();


                var trip = await CreateTripFromJsonAsync(json, session.UserId);
                Console.WriteLine($"Session Id = {session.Id}");
                Console.WriteLine($"UserId = {session.UserId}");


                session.TripId = trip.Id;
                session.Stage = ChatStage.PlanReady;

                try
                {
                    plan = await _orchestrator.BuildTripPlanAsync(trip.Id);
                    finalReply = $"تم إنشاء خطة رحلتك إلى {trip.Destination}! " +
                                 $"من {trip.StartDate} إلى {trip.EndDate}. " +
                                 $"{plan.Summary} " +
                                 "تقدر تعدل أي حاجة فيها وقولّي.";
                }
                catch (Exception ex)
                {
                    finalReply = $"تم إنشاء الرحلة، بس حصل خطأ في بناء الخطة: {ex.Message}";
                }
            }
            else if (rawReply.TrimStart().StartsWith("TRIP_UPDATE:"))
            {
                if (session.TripId == null)
                {
                    return new ChatReplyDto
                    {
                        Message = "مفيش رحلة موجودة  عشان تتعدل. نبدأ نعمل رحلة جديدة؟",
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

        public async Task<ChatSession> CreateSessionAsync(string userId)
        {
            //var existing = await _chatRepo.GetSessionByUserAsync(userId);
            //if (existing != null)
            //    return existing;
         

            var session =  await _chatRepo.CreateSessionAsync(userId);

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

        //private async Task<Trip> CreateTripFromJsonAsync(string json, string userId)
        //{

        //    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        //    var data = JsonSerializer.Deserialize<TripCreateDto>(json, options)
        //               ?? throw new Exception("Failed to parse trip data from AI");

        //    var profiles = await _userProfileRepo.FindAsync(p => p.AspNetUserId == userId);
        //    var userProfile = profiles.FirstOrDefault()
        //                      ?? throw new Exception("User profile not found");

        //    var trip = new Trip
        //    {
        //        Id = Guid.NewGuid(),
        //        UserId = userProfile.Id,
        //        Title = $"Trip to {data.Destination}",
        //        Destination = data.Destination,
        //        OriginCity = data.OriginCity,
        //        StartDate = DateOnly.Parse(data.StartDate),
        //        EndDate = DateOnly.Parse(data.EndDate),
        //        NumTravelers = data.NumTravelers,
        //        BudgetTotal = data.BudgetTotal,
        //        Status = TripStatus.Draft,
        //        CreatedAt = DateTime.UtcNow,
        //        Preferences = data.Preferences.Select(p => new TripPreference
        //        {
        //            Id = Guid.NewGuid(),
        //            Category = "General",
        //            Value = p
        //        }).ToList()
        //    };


        //    await _unitOfWork.Trips.AddAsync(trip);
        //    await _unitOfWork.CompleteAsync();
        //    return trip;
        //}
        private async Task<Trip> CreateTripFromJsonAsync(string json, string userId)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var data = JsonSerializer.Deserialize<TripCreateDto>(json, options)
                       ?? throw new Exception("Failed to parse trip data from AI");

            var profiles = await _userProfileRepo.FindAsync(p => p.AspNetUserId == userId);
            var userProfile = profiles.FirstOrDefault()
                              ?? throw new Exception("User profile not found");

            // 🔥 Normalize BEFORE creating Trip
            data.OriginCity = NormalizeCity(data.OriginCity);
            data.Destination = NormalizeCity(data.Destination);

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

            await _unitOfWork.Trips.AddAsync(trip);
            await _unitOfWork.CompleteAsync();

            return trip;
        }
        private string NormalizeCity(string city)
        {
            return city?.Trim().ToLower() switch
            {
                "القاهرة" or "cairo" => "CAI",
                "paris" => "CDG",
                "london" => "LHR",
                "dubai" => "DXB",
                _ => city
            };
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
    }
}