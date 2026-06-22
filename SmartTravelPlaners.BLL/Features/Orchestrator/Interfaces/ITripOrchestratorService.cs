using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces
{
    public interface ITripOrchestratorService
    {
        /// <summary>
        /// Builds a single complete trip plan (hotel + optional flight + day-by-day activities),
        /// persists it under the given Trip, and returns the structured plan.
        /// </summary>
        Task<TripPlanDto> BuildTripPlanAsync(Guid tripId);
        Task<TripHotelDto?> RegenerateHotelAsync(Guid tripId);
        Task<List<ActivityPlanDto>> RegenerateDayActivitiesAsync(Guid tripId, int dayNumber);
        Task SyncDayPlansAsync(Guid tripId);
        Task<TripFlightDto?> RegenerateFlightAsync(Guid tripId);
        Task<TripPlanDto> GetCurrentPlanAsync(Guid tripId);
        //Task<Trip?> GetTripWithDetailsNoTrackingAsync(Guid tripId);
    }
}
