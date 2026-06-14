using SmartTravelPlaners.BLL.ExternalApis.FlightAPI.DTOs;

namespace SmartTravelPlaners.BLL.ExternalApis.FlightAPI.Interfaces
{
    public interface IFlightService
    {
        // Search for one-way or round-trip flights based on TripType

        Task<FlightSearchResult> SearchFlightsAsync(FlightSearchRequest request);
    }
}