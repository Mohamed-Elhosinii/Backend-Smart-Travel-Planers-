using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<ChatService> _logger;

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
            WeatherPlugin weatherPlugin,
            ILogger<ChatService> logger)
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
            _logger = logger;
        }

        public async Task<ChatReplyDto> SendMessageAsync(Guid sessionId, string userId, string userMessage)
        {
            try
            {
                _logger.LogInformation("Processing chat message for SessionId: {SessionId}, UserId: {UserId}", sessionId, userId);

                var session = await _chatRepo.GetSessionAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("Chat session not found. SessionId: {SessionId}", sessionId);
                    throw new Exception("Session not found");
                }

                if (session.UserId != userId)
                {
                    _logger.LogWarning("Unauthorized access attempt. SessionId: {SessionId}, UserId: {UserId}", sessionId, userId);
                    throw new UnauthorizedAccessException("You do not have access to this session.");
                }

                // ===========================
                // USAGE LIMIT: Check message limit before calling the AI
                // ===========================
                var canSend = await _usageLimitService.CanSendMessageAsync(session.UserId);
                if (!canSend)
                {
                    _logger.LogWarning("Message limit reached for UserId: {UserId}", session.UserId);
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

                history.AddSystemMessage(@"CRITICAL RULES FOR TRIP CREATION AND LOCATIONS:
1. To create a new trip, you MUST explicitly collect ALL of the following from the user: Destination city, Origin city (departure), Start Date, End Date, Number of travelers, and Total budget. 
2. If ANY of these details are missing, DO NOT call create_trip. Instead, ask the user for the missing information. Never hallucinate or assume missing details (especially the destination or origin).
3. If the user provides a city name (for destination or origin) that is misspelled or has a typo, you MUST correct it and ask them 'Did you mean [Correct City Name]?' before proceeding or calling any tools.
4. If the user REJECTS your spelling suggestion (e.g. says 'no'), DO NOT proceed with the unrecognized or rejected city name. Instead, ask the user to type the place name correctly again.
5. 'origincity' and 'destination' MUST be specific CITY names, NEVER a country. If the user provides a country, ask them which city in that country.");

                history.AddSystemMessage(@"CRITICAL RULES FOR HOTELS:
1. NEVER invent, estimate, or hallucinate hotel data (Name, Price, PricePerNight, Lat, Lng, Address, BookingUrl, ImagesJson) when calling Trip-update_hotel.
2. For ANY hotel-related request (e.g., change hotel, cheaper hotel, closer hotel, similar hotel), you MUST first call a search tool: Hotel-search_hotels, Hotel-filter_hotels, Hotel-get_hotels_near_location, or Hotel-get_similar_hotels to get real data.
3. ONLY AFTER receiving real results from a search tool, you may call Trip-update_hotel using the EXACT literal values from the search result. Do not modify or round the values.
4. If the user asks for a cheaper hotel, use filter_hotels or search_hotels first, pick the actual cheapest from the real results, and then update.
5. If the user asks for a hotel near a specific location or coordinates, use get_hotels_near_location with the exact coordinates, then update using the first real result.
6. If no real results are found or the search fails, inform the user in their language that no matching hotel was found. DO NOT invent a hotel.
7. These rules apply ONLY to hotels. Do not apply them to flights or other features.
8. If the user asks whether a hotel is available for specific dates, call Hotel-check_hotel_availability with the city, hotel name/id, check-in, and check-out dates. Tell the user the result in their language.
9. If the user asks for more details about a specific hotel (amenities, photos, rating, etc.), call Hotel-get_hotel_details with the city and the hotel name.
10. When calling Hotel-get_similar_hotels or Hotel-get_hotel_details, always pass the hotel NAME as the identifier, NOT the hotel_id.");

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
- When the user says they want to change something but does NOT provide the new value, ask for it first IN THE USER'S LANGUAGE.
- Only call the tool AFTER you have the new value.
- For example: If the user wants to change the destination/origin without specifying, ask: 'Do you mean the destination or the origin city?' (in their language).
- If they say 'الوجهة' or 'destination', update the 'destination' field.
- If they say 'الانطلاق' or 'origin', update the 'origincity' field.
- 'origincity' MUST be a specific CITY name, NEVER a country. If user says 'Egypt' or 'مصر', ask them which city.
- If the user provides a city name with a typo or misspelling, you MUST ask them 'Did you mean [Correct City Name]?' before calling any tools.
- If the user REJECTS your spelling suggestion (e.g. says 'no'), DO NOT proceed with the unrecognized city name. Instead, ask the user to type the place name correctly again.
- If the user wants to change dates WITHOUT specifying which date (start or end), ask them first which date they want to change (in their language).
- If user confirms a change, just confirm the change was made in their language.
- CRITICAL FOR LOCATIONS: If a city name is ambiguous or famous (like 'Roma', 'Alexandria', 'Paris'), ALWAYS append its most likely country to your internal tool parameters (e.g., use 'Rome, Italy' instead of 'Roma') UNLESS the user specifies otherwise. This prevents searching in the wrong country.
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

                _logger.LogInformation("User message stored. Initiating LLM call for SessionId: {SessionId}", sessionId);

                _kernel.FunctionInvoking += (sender, args) =>
                {
                    _logger.LogInformation("Invoking semantic kernel function: {FunctionName}", args.Function.Name);
                };
                _kernel.FunctionInvoked += (sender, args) =>
                {
                    _logger.LogInformation("Semantic kernel function completed: {FunctionName}", args.Function.Name);
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

                    _logger.LogInformation("Starting LLM generation for SessionId: {SessionId}", sessionId);
                    result = await _ai.GetChatMessageContentAsync(history, settings, _kernel, cts.Token);
                    sw.Stop();
                    _logger.LogInformation("LLM call completed successfully in {ElapsedMilliseconds}ms for SessionId: {SessionId}", sw.ElapsedMilliseconds, sessionId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError("LLM call timeout for SessionId: {SessionId}", sessionId);
                    return new ChatReplyDto { Message = "Sorry, the AI service took too long to respond. Please try again later." };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LLM call failed for SessionId: {SessionId}. Error: {ErrorMessage}", sessionId, ex.Message);
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

                        _logger.LogInformation("Trip created and linked to session. TripId: {TripId}, SessionId: {SessionId}", newTrip.Id, sessionId);
                    }
                }

                // ===========================
                // POPULATE PLAN FOR FRONTEND
                // ===========================
                TripPlanDto? plan = null;
                if (_tripPlugin.IsPlanUpdating)
                {
                    session.Stage = ChatStage.Modifying;
                    _logger.LogInformation("Trip is being updated. SessionId: {SessionId}", sessionId);
                }
                else if (_tripPlugin.ShowPlanRequested && session.TripId != null)
                {
                    try
                    {
                        plan = await _orchestrator.GetCurrentPlanAsync(session.TripId.Value);
                        _logger.LogInformation("Trip plan retrieved. TripId: {TripId}", session.TripId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to retrieve trip plan. TripId: {TripId}. Error: {ErrorMessage}", session.TripId, ex.Message);
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

                _logger.LogInformation("Chat message processed successfully. SessionId: {SessionId}", sessionId);

                return new ChatReplyDto
                {
                    Message = finalReply,
                    Plan = plan,
                    TripId = session.TripId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message for SessionId: {SessionId}, UserId: {UserId}. Error: {ErrorMessage}", 
                    sessionId, userId, ex.Message);
                throw;
            }
        }

        public async Task<ChatSession> CreateSessionAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Creating new chat session for UserId: {UserId}", userId);

                var session = await _chatRepo.CreateSessionAsync(userId);
                await _chatRepo.SaveChangesAsync();

                _logger.LogInformation("Chat session created successfully. SessionId: {SessionId}, UserId: {UserId}", session.Id, userId);
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create chat session for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Retrieving chat sessions for UserId: {UserId}", userId);

                var sessions = await _chatRepo.GetSessionsByUserAsync(userId);

                _logger.LogInformation("Retrieved {SessionCount} chat sessions for UserId: {UserId}", sessions.Count, userId);
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve chat sessions for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId, string userId)
        {
            try
            {
                _logger.LogInformation("Retrieving chat history for SessionId: {SessionId}, UserId: {UserId}", sessionId, userId);

                var session = await _chatRepo.GetSessionAsync(sessionId);
                if (session == null || session.UserId != userId)
                {
                    _logger.LogWarning("Unauthorized access to chat history. SessionId: {SessionId}, UserId: {UserId}", sessionId, userId);
                    throw new UnauthorizedAccessException("You do not have access to this session.");
                }

                var history = await _chatRepo.GetMessagesAsync(sessionId);

                _logger.LogInformation("Retrieved {MessageCount} messages from session history. SessionId: {SessionId}", history.Count, sessionId);
                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve chat history. SessionId: {SessionId}, UserId: {UserId}. Error: {ErrorMessage}", 
                    sessionId, userId, ex.Message);
                throw;
            }
        }

        public async Task<TripPlanDto?> GetTripPlanAsync(Guid tripId, string userId)
        {
            try
            {
                _logger.LogInformation("Retrieving trip plan. TripId: {TripId}, UserId: {UserId}", tripId, userId);

                var profile = await _userProfileRepo.GetUserProfileWithPreferencesAsync(userId);
                if (profile == null)
                {
                    _logger.LogWarning("User profile not found. UserId: {UserId}", userId);
                    return null;
                }

                var trip = await _tripRepo.GetByIdAsync(tripId);
                if (trip == null || trip.UserId != profile.Id)
                {
                    _logger.LogWarning("Trip not found or unauthorized access. TripId: {TripId}, UserId: {UserId}", tripId, userId);
                    return null;
                }

                var plan = await _orchestrator.GetCurrentPlanAsync(tripId);
                _logger.LogInformation("Trip plan retrieved successfully. TripId: {TripId}", tripId);

                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve trip plan. TripId: {TripId}, UserId: {UserId}. Error: {ErrorMessage}", 
                    tripId, userId, ex.Message);
                return null;
            }
        }
        
        public async Task LinkSessionToTripAsync(Guid sessionId, string userId, Guid tripId)
        {
            try
            {
                _logger.LogInformation("Linking chat session to trip. SessionId: {SessionId}, UserId: {UserId}, TripId: {TripId}", 
                    sessionId, userId, tripId);

                var session = await _chatRepo.GetSessionAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("Chat session not found. SessionId: {SessionId}", sessionId);
                    throw new Exception("Session not found");
                }

                if (session.UserId != userId)
                {
                    _logger.LogWarning("Unauthorized access to session. SessionId: {SessionId}, UserId: {UserId}", sessionId, userId);
                    throw new UnauthorizedAccessException("You do not have access to this session.");
                }

                session.TripId = tripId;
                session.Stage = ChatStage.PlanReady;
                session.UpdatedAt = DateTime.UtcNow;

                await _chatRepo.SaveChangesAsync();

                _logger.LogInformation("Chat session linked to trip successfully. SessionId: {SessionId}, TripId: {TripId}", sessionId, tripId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to link session to trip. SessionId: {SessionId}, TripId: {TripId}. Error: {ErrorMessage}", 
                    sessionId, tripId, ex.Message);
                throw;
            }
        }

        public async Task<ChatSession> GetOrCreateTripSessionAsync(Guid tripId, string userId)
        {
            try
            {
                _logger.LogInformation("Getting or creating chat session for trip. TripId: {TripId}, UserId: {UserId}", tripId, userId);

                // لو فيه Session موجودة للرحلة، رجعها
                var existingSession = await _chatRepo.GetSessionByTripIdAsync(tripId, userId);

                if (existingSession != null)
                {
                    _logger.LogInformation("Existing chat session found for trip. TripId: {TripId}, SessionId: {SessionId}", tripId, existingSession.Id);
                    return existingSession;
                }

                // لو مفيش، اعمل Session جديدة
                var session = await _chatRepo.CreateSessionAsync(userId);

                session.TripId = tripId;
                session.Stage = ChatStage.PlanReady;

                await _chatRepo.SaveChangesAsync();

                _logger.LogInformation("New chat session created for trip. TripId: {TripId}, SessionId: {SessionId}, UserId: {UserId}", 
                    tripId, session.Id, userId);

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get or create chat session for trip. TripId: {TripId}, UserId: {UserId}. Error: {ErrorMessage}", 
                    tripId, userId, ex.Message);
                throw;
            }
        }
    }
}