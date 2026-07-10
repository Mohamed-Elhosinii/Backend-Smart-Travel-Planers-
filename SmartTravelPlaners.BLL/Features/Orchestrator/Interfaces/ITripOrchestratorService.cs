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
        Task<TripHotelDto?> SetSpecificHotelAsync(Guid tripId, string name, decimal pricePerNight, double lat, double lng, string address, double rating, string imagesJson);
        Task<List<TripFlightDto>> SetSpecificFlightAsync(Guid tripId, bool isReturnFlight, string airline, string flightNumber, string origin, string destination, string departureTime, string arrivalTime);
        Task<List<ActivityPlanDto>> RegenerateDayActivitiesAsync(Guid tripId, int dayNumber);
        Task SyncDayPlansAsync(Guid tripId, string? changedField = null);
        Task<List<TripFlightDto>> RegenerateFlightAsync(Guid tripId);
        Task RegenerateWeatherAsync(Guid tripId);
        Task<TripPlanDto> GetCurrentPlanAsync(Guid tripId);
        
    }
}
