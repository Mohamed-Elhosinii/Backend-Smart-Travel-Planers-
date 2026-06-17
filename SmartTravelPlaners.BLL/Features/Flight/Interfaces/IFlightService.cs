using SmartTravelPlaners.BLL.Features.Flight.DTOs;

namespace SmartTravelPlaners.BLL.Features.Flight.Interfaces
{
    public interface IFlightService
    {
        // Search for one-way or round-trip flights based on TripType
        Task<FlightSearchResult> SearchFlightsAsync(FlightSearchRequest request);

        // Resolve city or airport name to IATA code using AirLabs API
        Task<string> GetIataCodeAsync(string cityName);
    }
}