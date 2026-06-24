using Microsoft.Extensions.DependencyInjection;
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

        public TripCreationService(
            IUnitOfWork unitOfWork,
            IUsageLimitService usageLimitService,
            IServiceProvider serviceProvider)
        {
            _unitOfWork = unitOfWork;
            _usageLimitService = usageLimitService;
            _serviceProvider = serviceProvider;
        }

        public async Task<TripCreationResult> CreateAndBuildAsync(TripCreateDto dto, string userId)
        {
            // 1) USAGE LIMIT — the SAME check the chat runs before creating a trip.
            var canTrip = await _usageLimitService.CanGenerateTripAsync(userId);
            if (!canTrip)
            {
                return new TripCreationResult
                {
                    LimitReached = true,
                    Message = LimitReachedMessage
                };
            }

            // 2) CREATE the Draft trip (same creation code path as the chat's TRIP_READY).
            var trip = await CreateTripAsync(dto, userId);

            // 3) BACKGROUND ORCHESTRATOR CALL — same fire-and-forget Task.Run pattern as today.
            var tripId = trip.Id;
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

            return new TripCreationResult
            {
                TripId = trip.Id,
                Trip = trip
            };
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
