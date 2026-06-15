using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;

namespace SmartTravelPlaners.BLL.Features.Flight.Plugins
{
    public class FlightPlugin
    {
        private readonly IFlightService _flightService;

        public FlightPlugin(IFlightService flightService)
        {
            _flightService = flightService;
        }

        [KernelFunction("search_flights")]
        [Description("Search for available flights between two airports on a specific date. Use this when the user asks about flights, travel options, or trip planning.")]
      
        public async Task<string> SearchFlightsAsync(
            [Description("Departure airport IATA code e.g. CAI for Cairo")] string departure,
            [Description("Arrival airport IATA code e.g. DXB for Dubai")] string arrival,
            [Description("Departure date in yyyy-MM-dd format")] string departureDate,
            [Description("Trip type: OneWay or RoundTrip")] string tripType = "OneWay",
            [Description("Return date in yyyy-MM-dd format, required only for RoundTrip")] string? returnDate = null)
        {
            var request = new FlightSearchRequest
            {
                DepartureAirport = departure.ToUpper(),
                ArrivalAirport = arrival.ToUpper(),
                DepartureDate = departureDate,
                TripType = Enum.Parse<TripType>(tripType, ignoreCase: true),
                ReturnDate = returnDate
            };

            var result = await _flightService.SearchFlightsAsync(request);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}