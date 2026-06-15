using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;

namespace SmartTravelPlaners.BLL.Features.Flight.Services
{
    public class FlightService : IFlightService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "c91dbf09d0msh0fcf2521d3dc089p13afb5jsn5bc15c3837fd";

        // IATA to ICAO airport code mapping
        private static readonly Dictionary<string, string> IataToIcao =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "CAI", "HECA" },
            { "DXB", "OMDB" },
            { "LHR", "EGLL" },
            { "CDG", "LFPG" },
            { "JFK", "KJFK" },
            { "IST", "LTFM" },
            { "RUH", "OERK" },
            { "JED", "OEJN" },
            { "DOH", "OTHH" },
            { "AUH", "OMAA" },
            { "AMM", "OJAI" },
            { "BEY", "OLBA" },
        };

        public FlightService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<FlightSearchResult> SearchFlightsAsync(FlightSearchRequest request)
        {
            // Always fetch outbound flights
            var outbound = await GetFlightsAsync(
                request.DepartureAirport,
                request.ArrivalAirport,
                request.DepartureDate
            );

            // Fetch return flights only if RoundTrip
            List<FlightDto>? returnFlights = null;

            if (request.TripType == TripType.RoundTrip)
            {
                if (string.IsNullOrWhiteSpace(request.ReturnDate))
                    throw new Exception("ReturnDate is required for RoundTrip");

                returnFlights = await GetFlightsAsync(
                    request.ArrivalAirport,
                    request.DepartureAirport,
                    request.ReturnDate
                );
            }

            return new FlightSearchResult
            {
                OutboundFlights = outbound,
                ReturnFlights = returnFlights
            };
        }

        // Fetch flights for a full day by splitting into two 12-hour windows
        private async Task<List<FlightDto>> GetFlightsAsync(
            string departureIata,
            string arrivalIata,
            string date)
        {
            var icao = GetIcao(departureIata);

            var morning = await FetchFromApi(icao, arrivalIata, departureIata,
                $"{date}T00:00", $"{date}T12:00");

            var evening = await FetchFromApi(icao, arrivalIata, departureIata,
                $"{date}T12:00", $"{date}T23:59");

            return morning.Concat(evening).ToList();
        }

        // Call the AeroDataBox API and filter results by arrival airport
        private async Task<List<FlightDto>> FetchFromApi(
            string icao,
            string arrivalIata,
            string departureIata,
            string fromTime,
            string toTime)
        {
            var url =
                $"https://aerodatabox.p.rapidapi.com/flights/airports/icao/{icao}/{fromTime}/{toTime}" +
                "?direction=Departure" +
                "&withLeg=true" +
                "&withCancelled=false" +
                "&withCodeshared=false" +
                "&withCargo=false" +
                "&withPrivate=false";

            var requestMsg = new HttpRequestMessage(HttpMethod.Get, url);
            requestMsg.Headers.Add("x-rapidapi-key", _apiKey);
            requestMsg.Headers.Add("x-rapidapi-host", "aerodatabox.p.rapidapi.com");

            var response = await _httpClient.SendAsync(requestMsg);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"API Error ({response.StatusCode}): {content}");

            var json = JsonDocument.Parse(content);

            if (!json.RootElement.TryGetProperty("departures", out var departures))
                return new List<FlightDto>();

            var flights = new List<FlightDto>();

            foreach (var item in departures.EnumerateArray())
            {
                try
                {
                    // Filter by arrival airport
                    var arrIata = GetString(item, "arrival", "airport", "iata");
                    if (!string.IsNullOrWhiteSpace(arrivalIata) &&
                        !arrIata.Equals(arrivalIata, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip cargo flights
                    if (item.TryGetProperty("isCargo", out var isCargoNode) &&
                        isCargoNode.GetBoolean())
                        continue;

                    flights.Add(new FlightDto
                    {
                        FlightNumber = GetString(item, "number"),
                        AirlineName = GetString(item, "airline", "name"),
                        DepartureAirport = departureIata,
                        ArrivalAirport = arrIata,
                        DepartureTime = GetString(item, "departure", "scheduledTime", "local"),
                        ArrivalTime = GetString(item, "arrival", "scheduledTime", "local"),
                        Status = GetString(item, "status"),
                        AircraftModel = GetString(item, "aircraft", "model"),
                        DepartureTerminal = GetString(item, "departure", "terminal"),
                        ArrivalTerminal = GetString(item, "arrival", "terminal"),
                    });
                }
                catch { continue; }
            }

            return flights;
        }

        // Convert IATA code to ICAO code
        private string GetIcao(string iata)
        {
            if (IataToIcao.TryGetValue(iata, out var icao))
                return icao;

            throw new Exception(
                $"ICAO code not found for airport: {iata} — please add it to IataToIcao dictionary");
        }

        // Safely navigate nested JSON properties
        private string GetString(JsonElement element, params string[] path)
        {
            var current = element;
            foreach (var key in path)
            {
                if (!current.TryGetProperty(key, out current))
                    return "";
            }
            return current.GetString() ?? "";
        }
    }
}