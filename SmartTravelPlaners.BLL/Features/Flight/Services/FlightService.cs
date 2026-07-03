
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;

namespace SmartTravelPlaners.BLL.Features.Flight.Services
{
    public class FlightService : IFlightService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FlightService> _logger;

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

        public FlightService(HttpClient httpClient, ILogger<FlightService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        // ============================================================
        // Resolve city name to IATA code using AirLabs Suggest API
        // ============================================================
        public async Task<string> GetIataCodeAsync(string cityName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cityName))
                {
                    _logger.LogWarning("Empty city name provided for IATA code lookup");
                    throw new Exception("City name cannot be empty");
                }

                if (CountryToCityIataFallback.TryGetValue(cityName.Trim(), out var fallbackIata))
                {
                    _logger.LogInformation("IATA code resolved from fallback map. City: {CityName}, IATA: {IATA}", cityName, fallbackIata);
                    return fallbackIata;
                }

                _logger.LogInformation("Looking up IATA code for city: {CityName}", cityName);

                var url = $"https://airlabs.co/api/v9/suggest?query={Uri.EscapeDataString(cityName)}&api_key={_airlabsApiKey}";

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("AirLabs API error for city: {CityName}, Status: {StatusCode}, Response: {Response}", 
                        cityName, response.StatusCode, content);
                    throw new Exception($"AirLabs API Error ({response.StatusCode}): {content}");
                }

                var json = JsonDocument.Parse(content);

                // Get airports array from response
                if (!json.RootElement.TryGetProperty("response", out var responseNode) ||
                    !responseNode.TryGetProperty("airports", out var airports))
                {
                    _logger.LogWarning("No airports found in AirLabs response for city: {CityName}", cityName);
                    throw new Exception($"No airports found for: {cityName}");
                }

                // Get the most popular airport (first result sorted by popularity)
                foreach (var airport in airports.EnumerateArray())
                {
                    if (airport.TryGetProperty("iata_code", out var iataNode) &&
                        iataNode.ValueKind != JsonValueKind.Null)
                    {
                        var iata = iataNode.GetString();
                        if (!string.IsNullOrWhiteSpace(iata))
                        {
                            _logger.LogInformation("IATA code resolved successfully. City: {CityName}, IATA: {IATA}", cityName, iata.ToUpper());
                            return iata.ToUpper();
                        }
                    }
                }

                _logger.LogWarning("No valid IATA code found in response for city: {CityName}", cityName);
                throw new Exception($"No IATA code found for: {cityName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve IATA code for city: {CityName}. Error: {ErrorMessage}", cityName, ex.Message);
                throw;
            }
        }

        // ============================================================
        // Search flights - accepts city names and resolves to IATA
        // ============================================================
        public async Task<FlightSearchResult> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Flight search initiated. From: {DepartureCity}, To: {ArrivalCity}, Date: {DepartureDate}, Type: {TripType}", 
                    request.DepartureCity, request.ArrivalCity, request.DepartureDate, request.TripType);

                var departureIata = await GetIataCodeAsync(request.DepartureCity);
                await Task.Delay(1200);

                var arrivalIata = await GetIataCodeAsync(request.ArrivalCity);
                await Task.Delay(1200);

                var outbound = await GetFlightsAsync(departureIata, arrivalIata, request.DepartureDate);

                List<FlightDto>? returnFlights = null;

                if (request.TripType == TripType.RoundTrip)
                {
                    if (string.IsNullOrWhiteSpace(request.ReturnDate))
                    {
                        _logger.LogWarning("ReturnDate missing for RoundTrip flight search");
                        throw new Exception("ReturnDate is required for RoundTrip");
                    }

                    await Task.Delay(1200);
                    returnFlights = await GetFlightsAsync(arrivalIata, departureIata, request.ReturnDate);
                }

                _logger.LogInformation("Flight search completed. Outbound flights: {OutboundCount}, Return flights: {ReturnCount}", 
                    outbound.Count, returnFlights?.Count ?? 0);

                return new FlightSearchResult
                {
                    OutboundFlights = outbound,
                    ReturnFlights = returnFlights,
                    DepartureIata = departureIata,
                    ArrivalIata = arrivalIata
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Flight search failed. From: {DepartureCity}, To: {ArrivalCity}. Error: {ErrorMessage}", 
                    request.DepartureCity, request.ArrivalCity, ex.Message);
                throw;
            }
        }


        private async Task<List<FlightDto>> GetFlightsAsync(
     string departureIata,
     string arrivalIata,
     string date)
        {
            try
            {
                _logger.LogInformation("Fetching flights for route. From: {DepartureIata}, To: {ArrivalIata}, Date: {Date}", 
                    departureIata, arrivalIata, date);

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

                var result = morning.Concat(evening).ToList();
                _logger.LogInformation("Flights fetched successfully. Total: {FlightCount}, From: {DepartureIata}, To: {ArrivalIata}, Date: {Date}", 
                    result.Count, departureIata, arrivalIata, date);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch flights. From: {DepartureIata}, To: {ArrivalIata}, Date: {Date}. Error: {ErrorMessage}", 
                    departureIata, arrivalIata, date, ex.Message);
                throw;
            }
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
            try
            {
                _logger.LogInformation("Calling AeroDataBox API. ICAO: {ICAO}, ArrivalIata: {ArrivalIata}, From: {FromTime}, To: {ToTime}", 
                    icao, arrivalIata, fromTime, toTime);

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
                {
                    _logger.LogError("AeroDataBox API error. ICAO: {ICAO}, Status: {StatusCode}, Response: {Response}", 
                        icao, response.StatusCode, content);
                    throw new Exception($"AeroDataBox API Error ({response.StatusCode}): {content}");
                }

                var json = JsonDocument.Parse(content);

                if (!json.RootElement.TryGetProperty("departures", out var departures))
                {
                    _logger.LogInformation("No departures found in API response. ICAO: {ICAO}", icao);
                    return new List<FlightDto>();
                }

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

                _logger.LogInformation("Flights parsed successfully. Count: {FlightCount}, ArrivalAirport: {ArrivalAirport}", 
                    flights.Count, arrivalIata);

                return flights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch flights from AeroDataBox API. ICAO: {ICAO}. Error: {ErrorMessage}", icao, ex.Message);
                throw;
            }
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