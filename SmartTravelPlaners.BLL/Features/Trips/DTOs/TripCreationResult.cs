using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.BLL.Features.Trips.DTOs
{
    /// <summary>
    /// Outcome of a trip-creation request, shared by the chat (TRIP_READY) and the
    /// form (quick-plan) callers so each can shape its own response.
    /// </summary>
    public class TripCreationResult
    {
        /// <summary>True when the monthly trip limit was reached and no trip was created.</summary>
        public bool LimitReached { get; init; }

        /// <summary>User-facing message when <see cref="LimitReached"/> is true.</summary>
        public string? Message { get; init; }

        /// <summary>The created Draft trip's id (null when <see cref="LimitReached"/>).</summary>
        public Guid? TripId { get; init; }

        /// <summary>The created Draft trip entity (null when <see cref="LimitReached"/>).</summary>
        public Trip? Trip { get; init; }
    }
}
