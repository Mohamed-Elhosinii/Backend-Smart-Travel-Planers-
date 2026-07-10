using SmartTravelPlaners.BLL.Features.Flight.DTOs;

namespace SmartTravelPlaners.BLL.Features.Flight.Interfaces
{
    public interface IFlightService
    {
        // Search for one-way or round-trip flights based on TripType
        Task<FlightSearchResult> SearchFlightsAsync(FlightSearchRequest request);

        // Resolve city or airport name to IATA and ICAO codes using AirLabs API
        Task<(string Iata, string Icao)> GetAirportCodesAsync(string cityName);
    }
}