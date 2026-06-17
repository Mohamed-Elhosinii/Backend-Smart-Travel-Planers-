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
        [Description("Search for available flights between two cities on a specific date. Use this when the user asks about flights, travel options, or trip planning.")]
        public async Task<string> SearchFlightsAsync(
            [Description("Departure city name e.g. Cairo, Dubai, London")] string departureCity,
            [Description("Arrival city name e.g. Dubai, London, Paris")] string arrivalCity,
            [Description("Departure date in yyyy-MM-dd format")] string departureDate,
            [Description("Trip type: OneWay or RoundTrip")] string tripType = "OneWay",
            [Description("Return date in yyyy-MM-dd format, required only for RoundTrip")] string? returnDate = null)
        {
            var request = new FlightSearchRequest
            {
                DepartureCity = departureCity,
                ArrivalCity = arrivalCity,
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

        [KernelFunction("get_airport_code")]
        [Description("Get the IATA airport code for a city name. Use this to resolve city names to airport codes.")]
        public async Task<string> GetAirportCodeAsync(
            [Description("City or airport name e.g. Cairo, Dubai, London")] string cityName)
        {
            var iata = await _flightService.GetIataCodeAsync(cityName);
            return $"The IATA code for {cityName} is {iata}";
        }
    }
}