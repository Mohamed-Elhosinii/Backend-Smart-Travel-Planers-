
using System.Text.Json;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;

namespace SmartTravelPlaners.BLL.Features.Flight.Services
{
    public class FlightService : IFlightService
    {
        private readonly HttpClient _httpClient;

        // AeroDataBox API credentials
        private readonly string _aeroApiKey = "13e422650dmsh4bab4335bf19a98p13c492jsn69f4bac78d92";
       

        // AirLabs API credentials
        private readonly string _airlabsApiKey = "fbc05fb5-6fbb-4cb7-b06f-c0fe2a09c362";

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

        private static readonly Dictionary<string, string> CountryToCityIataFallback =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "Egypt", "CAI" },
            { "مصر", "CAI" },
            { "UAE", "DXB" },
            { "United Arab Emirates", "DXB" },
            { "الامارات", "DXB" },
            { "الإمارات", "DXB" },
            { "Saudi Arabia", "RUH" },
            { "KSA", "RUH" },
            { "السعودية", "RUH" },
            { "Turkey", "IST" },
            { "تركيا", "IST" },
            { "France", "CDG" },
            { "فرنسا", "CDG" },
            { "UK", "LHR" },
            { "United Kingdom", "LHR" },
            { "بريطانيا", "LHR" }
        };

        public FlightService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ============================================================
        // Resolve city name to IATA code using AirLabs Suggest API
        // ============================================================
        public async Task<string> GetIataCodeAsync(string cityName)
        {
            if (string.IsNullOrWhiteSpace(cityName))
                throw new Exception("City name cannot be empty");

            if (CountryToCityIataFallback.TryGetValue(cityName.Trim(), out var fallbackIata))
            {
                return fallbackIata;
            }

            var url = $"https://airlabs.co/api/v9/suggest?query={Uri.EscapeDataString(cityName)}&api_key={_airlabsApiKey}";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"AirLabs API Error ({response.StatusCode}): {content}");

            var json = JsonDocument.Parse(content);

            // Get airports array from response
            if (!json.RootElement.TryGetProperty("response", out var responseNode) ||
                !responseNode.TryGetProperty("airports", out var airports))
                throw new Exception($"No airports found for: {cityName}");

            // Get the most popular airport (first result sorted by popularity)
            foreach (var airport in airports.EnumerateArray())
            {
                if (airport.TryGetProperty("iata_code", out var iataNode) &&
                    iataNode.ValueKind != JsonValueKind.Null)
                {
                    var iata = iataNode.GetString();
                    if (!string.IsNullOrWhiteSpace(iata))
                        return iata.ToUpper();
                }
            }

            throw new Exception($"No IATA code found for: {cityName}");
        }

        // ============================================================
        // Search flights - accepts city names and resolves to IATA
        // ============================================================
        public async Task<FlightSearchResult> SearchFlightsAsync(FlightSearchRequest request)
        {
            var departureIata = await GetIataCodeAsync(request.DepartureCity);
            await Task.Delay(1200);

            var arrivalIata = await GetIataCodeAsync(request.ArrivalCity);
            await Task.Delay(1200);

            var outbound = await GetFlightsAsync(departureIata, arrivalIata, request.DepartureDate);

            List<FlightDto>? returnFlights = null;

            if (request.TripType == TripType.RoundTrip)
            {
                if (string.IsNullOrWhiteSpace(request.ReturnDate))
                    throw new Exception("ReturnDate is required for RoundTrip");

                await Task.Delay(1200);
                returnFlights = await GetFlightsAsync(arrivalIata, departureIata, request.ReturnDate);
            }

            return new FlightSearchResult
            {
                OutboundFlights = outbound,
                ReturnFlights = returnFlights,
                DepartureIata = departureIata,
                ArrivalIata = arrivalIata
            };
        }


        private async Task<List<FlightDto>> GetFlightsAsync(
     string departureIata,
     string arrivalIata,
     string date)
        {
            var icao = GetIcao(departureIata);

            var morning = await FetchFromApi(
                icao,
                arrivalIata,
                departureIata,
                $"{date}T00:00",
                $"{date}T11:00");

            await Task.Delay(1200);

            var evening = await FetchFromApi(
                icao,
                arrivalIata,
                departureIata,
                $"{date}T11:00",
                $"{date}T23:00");

            return morning.Concat(evening).ToList();
        }

        // ============================================================
        // Call AeroDataBox API and filter results by arrival airport
        // ============================================================
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
            requestMsg.Headers.Add("x-rapidapi-key", _aeroApiKey);
            requestMsg.Headers.Add("x-rapidapi-host", "aerodatabox.p.rapidapi.com");

            var response = await _httpClient.SendAsync(requestMsg);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"AeroDataBox API Error ({response.StatusCode}): {content}");

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