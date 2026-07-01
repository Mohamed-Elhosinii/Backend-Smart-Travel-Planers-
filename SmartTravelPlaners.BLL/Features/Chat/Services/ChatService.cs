using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Interfaces;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Plugins;
using SmartTravelPlaners.BLL.Features.Flight.Plugins;
using SmartTravelPlaners.BLL.Features.Hotel.Plugins;
using SmartTravelPlaners.BLL.Features.Place.Plugins;
using SmartTravelPlaners.BLL.Features.Weather.Plugins;
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
        private readonly Kernel _kernel;
        private readonly TripPlugin _tripPlugin;
        private readonly FlightPlugin _flightPlugin;
        private readonly HotelPlugin _hotelPlugin;
        private readonly PlacesPlugin _placesPlugin;
        private readonly WeatherPlugin _weatherPlugin;
        private readonly ITripOrchestratorService _orchestrator;
        private readonly IUsageLimitService _usageLimitService;

        public ChatService(
            IChatRepository chatRepo,
            ITripRepository tripRepo,
            IUserProfileRepository userProfileRepo,
            ITripOrchestratorService orchestrator,
            IUsageLimitService usageLimitService,
            Kernel kernel,
            TripPlugin tripPlugin,
            FlightPlugin flightPlugin,
            HotelPlugin hotelPlugin,
            PlacesPlugin placesPlugin,
            WeatherPlugin weatherPlugin)
        {
            _chatRepo = chatRepo;
            _tripRepo = tripRepo;
            _userProfileRepo = userProfileRepo;
            _kernel = kernel;
            _ai = kernel.GetRequiredService<IChatCompletionService>();
            _tripPlugin = tripPlugin;
            _flightPlugin = flightPlugin;
            _hotelPlugin = hotelPlugin;
            _placesPlugin = placesPlugin;
            _weatherPlugin = weatherPlugin;
            _orchestrator = orchestrator;
            _usageLimitService = usageLimitService;
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

            // ===========================
            // CONFIGURE TRIP PLUGIN STATE
            // ===========================
            _tripPlugin.UserId = session.UserId;
            _tripPlugin.TripId = session.TripId;
            _tripPlugin.IsPlanUpdating = false;
            _tripPlugin.ShowPlanRequested = false;

            if (!_kernel.Plugins.Contains("Trip"))
            {
                _kernel.Plugins.AddFromObject(_tripPlugin, "Trip");
            }
            if (!_kernel.Plugins.Contains("Flight"))
            {
                _kernel.Plugins.AddFromObject(_flightPlugin, "Flight");
            }
            if (!_kernel.Plugins.Contains("Hotel"))
            {
                _kernel.Plugins.AddFromObject(_hotelPlugin, "Hotel");
            }
            if (!_kernel.Plugins.Contains("Places"))
            {
                _kernel.Plugins.AddFromObject(_placesPlugin, "Places");
            }
            if (!_kernel.Plugins.Contains("Weather"))
            {
                _kernel.Plugins.AddFromObject(_weatherPlugin, "Weather");
            }

            var history = new ChatHistory();

            // ===========================
            // SYSTEM PROMPTS
            // ===========================
            history.AddSystemMessage(@"You are a smart travel assistant called TravelBot.
Talk to the user in the SAME LANGUAGE they use to talk to you, in a friendly and natural way.
Your job is to collect travel information to plan a new trip, or help them modify an existing trip plan.
You have access to a variety of tools (create_trip, update_trip_field, update_hotel, update_flight, update_activities, show_trip) as well as search tools for flights, hotels, places, and weather.
Use these tools to take any trip actions on behalf of the user. 
IMPORTANT: After calling a tool, ALWAYS reply to the user in natural conversational language in their own language, summarizing what happened. NEVER expose raw JSON or tool output directly to the user.");

            // Inject current date
            history.AddSystemMessage($"Today's date is {DateTime.UtcNow:yyyy-MM-dd}. Use this to validate any dates the user or trip mentions — reject any date before today.");

            if (session.TripId != null)
            {
                var trip = await _tripRepo.GetByIdAsync(session.TripId.Value);
                history.AddSystemMessage($@"
The user already has an active trip.
Current trip details:
Destination: {trip.Destination}
Origin: {trip.OriginCity}
Start Date: {trip.StartDate}
End Date: {trip.EndDate}
Travelers: {trip.NumTravelers}
Budget: {trip.BudgetTotal}

IMPORTANT RULES:
- The user is referring to THIS trip ONLY.
- Never ask for the destination or any trip details again unless they want to change them.
- Never ask for confirmation before making changes; just use the update_trip_field tool directly.
- When the user says they want to change something but does NOT provide the new value, ask for it first in Arabic.
- Only call the tool AFTER you have the new value.
- For example: If the user says 'عدل الدولة' or 'غير الدولة' without specifying, ask: 'هل تقصد دولة الوجهة أم مدينة الانطلاق؟'
- If they say 'الوجهة' or 'الذهاب', update the 'destination' field.
- If they say 'الانطلاق' or 'المغادرة', update the 'origincity' field.
- 'origincity' MUST be a specific CITY name, NEVER a country. If user says 'Egypt' or 'مصر', ask them 'من أي مدينة في مصر؟'.
- If the user wants to change dates WITHOUT specifying which date (start or end), ask them first: 'هل تريد تغيير تاريخ البداية أم تاريخ العودة؟'
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

            Console.WriteLine("DEBUG BACKEND: Received message from user, initiating LLM call...");

            _kernel.FunctionInvoking += (sender, args) =>
            {
                Console.WriteLine($"[SK HOOK] Invoking function: {args.Function.Name} at {DateTime.UtcNow:HH:mm:ss.fff}");
            };
            _kernel.FunctionInvoked += (sender, args) =>
            {
                Console.WriteLine($"[SK HOOK] Function {args.Function.Name} completed at {DateTime.UtcNow:HH:mm:ss.fff}");
            };

            Microsoft.SemanticKernel.ChatMessageContent result = null;
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)); 
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var settings = new OpenAIPromptExecutionSettings 
                { 
                    // By default, Auto() sets maximumAutoInvokeAttempts = 5.
                    // Meaning the LLM can recursively call up to 5 functions sequentially before returning to the user.
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() 
                };

                Console.WriteLine($"[LLM START] Attempting AI generation at {DateTime.UtcNow:HH:mm:ss.fff}");
                result = await _ai.GetChatMessageContentAsync(history, settings, _kernel, cts.Token);
                sw.Stop();
                Console.WriteLine($"[LLM END] LLM call completed successfully in {sw.ElapsedMilliseconds}ms.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("DEBUG BACKEND ERROR: LLM call timed out.");
                return new ChatReplyDto { Message = "Sorry, the AI service took too long to respond. Please try again later." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG BACKEND ERROR: LLM call failed or timed out: {ex.Message}");
                return new ChatReplyDto { Message = "Sorry, the AI service is currently unavailable. Please try again later." };
            }

            var finalReply = result?.Content ?? "Sorry, unable to respond right now";

            // ===========================
            // SYNC NEW TRIP STATE
            // ===========================
            if (_tripPlugin.TripId.HasValue && session.TripId == null)
            {
                session.TripId = _tripPlugin.TripId;
                session.Stage = ChatStage.PlanReady;
                
                var newTrip = await _tripRepo.GetByIdAsync(session.TripId.Value);
                if (newTrip != null)
                {
                    session.Title = string.IsNullOrWhiteSpace(newTrip.OriginCity) 
                        ? $"رحلة إلى {newTrip.Destination}" 
                        : $"رحلة من {newTrip.OriginCity} إلى {newTrip.Destination}";
                }
            }

            // ===========================
            // POPULATE PLAN FOR FRONTEND
            // ===========================
            TripPlanDto? plan = null;
            if (_tripPlugin.IsPlanUpdating)
            {
                session.Stage = ChatStage.Modifying;
            }
            else if (_tripPlugin.ShowPlanRequested && session.TripId != null)
            {
                try
                {
                    plan = await _orchestrator.GetCurrentPlanAsync(session.TripId.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetCurrentPlanAsync failed: {ex.Message}");
                }
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