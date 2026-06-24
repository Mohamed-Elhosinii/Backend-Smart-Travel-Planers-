using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Trips.DTOs;

namespace SmartTravelPlaners.BLL.Features.Trips.Interfaces
{
    /// <summary>
    /// Single, shared trip-creation pipeline used by BOTH the chat <c>TRIP_READY</c>
    /// handler and the form-driven <c>POST /api/Trip/quick-plan</c> endpoint:
    /// enforce the monthly trip limit → create a Draft <c>Trip</c> → fire the background
    /// <c>BuildTripPlanAsync</c> → increment trip usage. There is exactly one of each.
    /// </summary>
    public interface ITripCreationService
    {
        /// <summary>
        /// Checks the monthly trip limit; if exceeded, returns a result with
        /// <see cref="TripCreationResult.LimitReached"/> = true and no trip created.
        /// Otherwise creates a Draft trip, starts the background plan build
        /// (fire-and-forget), and returns the new trip id immediately.
        /// </summary>
        Task<TripCreationResult> CreateAndBuildAsync(TripCreateDto dto, string userId);
    }
}
