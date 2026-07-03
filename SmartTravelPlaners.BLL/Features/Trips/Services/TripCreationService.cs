using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.DTOs;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Trips.Services
{
    /// <summary>
    /// The single source of truth for "create a Draft trip, then build its plan in the
    /// background". Extracted from the chat's TRIP_READY handler so the chat and the
    /// form-driven quick-plan endpoint share ONE creation + usage + background-build path
    /// (one limit check, one persistence path, one BuildTripPlanAsync, one usage increment).
    /// </summary>
    public class TripCreationService : ITripCreationService
    {
        /// <summary>Same message the chat returned inline when the trip limit was hit.</summary>
        public const string LimitReachedMessage =
            "You've reached your monthly trip generation limit. Upgrade your plan to create more trips! 🚀";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IUsageLimitService _usageLimitService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IChatRepository _chatRepo;
        private readonly ILogger<TripCreationService> _logger;

        public TripCreationService(
            IUnitOfWork unitOfWork,
            IUsageLimitService usageLimitService,
            IServiceProvider serviceProvider,
            IChatRepository chatRepo,
            ILogger<TripCreationService> logger)
        {
            _unitOfWork = unitOfWork;
            _usageLimitService = usageLimitService;
            _serviceProvider = serviceProvider;
            _chatRepo = chatRepo;
            _logger = logger;
        }

        public async Task<TripCreationResult> CreateAndBuildAsync(TripCreateDto dto, string userId)
        {
            try
            {
                _logger.LogInformation("Trip creation initiated. UserId: {UserId}, Destination: {Destination}", userId, dto.Destination);

                // 1) USAGE LIMIT — the SAME check the chat runs before creating a trip.
                var canTrip = await _usageLimitService.CanGenerateTripAsync(userId);
                if (!canTrip)
                {
                    _logger.LogWarning("Trip creation limit reached for UserId: {UserId}", userId);
                    return new TripCreationResult
                    {
                        LimitReached = true,
                        Message = LimitReachedMessage
                    };
                }

                // 2) CREATE the Draft trip (same creation code path as the chat's TRIP_READY).
                var trip = await CreateTripAsync(dto, userId);
                _logger.LogInformation("Trip created successfully. TripId: {TripId}, UserId: {UserId}, Destination: {Destination}", 
                    trip.Id, userId, trip.Destination);

                try
                {
                    // Create session linking to the newly created trip immediately
                    var session = await _chatRepo.CreateSessionAsync(userId);
                    session.TripId = trip.Id;
                    session.Stage = ChatStage.PlanReady;
                    session.Title = trip.Title; // Will be "Trip to {Destination}"
                    await _chatRepo.SaveChangesAsync();

                    _logger.LogInformation("Chat session created and linked to trip. SessionId: {SessionId}, TripId: {TripId}", session.Id, trip.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create initial ChatSession for trip {TripId}. It will be created lazily later. Error: {ErrorMessage}", 
                        trip.Id, ex.Message);
                }

                // 3) BACKGROUND ORCHESTRATOR CALL — same fire-and-forget Task.Run pattern as today.
                var tripId = trip.Id;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Background orchestrator started for TripId: {TripId}", tripId);

                        using var scope = _serviceProvider.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                        var usageService = scope.ServiceProvider.GetRequiredService<IUsageLimitService>();

                        await orchestrator.BuildTripPlanAsync(tripId);
                        _logger.LogInformation("Trip plan built successfully in background. TripId: {TripId}", tripId);

                        await usageService.IncrementTripUsageAsync(userId);
                        _logger.LogInformation("Trip usage incremented. UserId: {UserId}, TripId: {TripId}", userId, tripId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background orchestrator failed for TripId: {TripId}. Error: {ErrorMessage}", tripId, ex.Message);
                    }
                });

                _logger.LogInformation("Trip creation completed. TripId: {TripId}, UserId: {UserId}", trip.Id, userId);

                return new TripCreationResult
                {
                    TripId = trip.Id,
                    Trip = trip
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trip creation failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        // Verbatim from the chat's CreateTripFromJsonAsync — minus the JSON deserialization,
        // since both callers now supply an already-parsed/bound TripCreateDto.
        private async Task<Trip> CreateTripAsync(TripCreateDto data, string userId)
        {
            var profiles = await _unitOfWork.UserProfiles.FindAsync(p => p.AspNetUserId == userId);

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
    }
}
