using SmartTravelPlaners.BLL.ExternalApis.DTOs;

namespace SmartTravelPlaners.BLL.ExternalApis.Interfaces
{
    public interface IFlightService
    {
        /// <summary>
        /// Search for one-way or round-trip flights based on TripType
        /// </summary>
        Task<FlightSearchResult> SearchFlightsAsync(FlightSearchRequest request);
    }
}