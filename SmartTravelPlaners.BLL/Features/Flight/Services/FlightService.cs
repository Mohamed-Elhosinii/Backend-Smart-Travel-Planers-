
using System.Text.Json;
using Microsoft.Extensions.Configuration;
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
        private readonly string _aeroApiKey;


        // AirLabs API credentials
        private readonly string _airlabsApiKey;


        private static readonly Dictionary<string, (string Iata, string Icao)> CountryToCityIataFallback =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "Egypt", ("CAI", "HECA") },
            { "مصر", ("CAI", "HECA") },
            { "UAE", ("DXB", "OMDB") },
            { "United Arab Emirates", ("DXB", "OMDB") },
            { "الامارات", ("DXB", "OMDB") },
            { "الإمارات", ("DXB", "OMDB") },
            { "Saudi Arabia", ("RUH", "OERK") },
            { "KSA", ("RUH", "OERK") },
            { "السعودية", ("RUH", "OERK") },
            { "Turkey", ("IST", "LTFM") },
            { "تركيا", ("IST", "LTFM") },
            { "France", ("CDG", "LFPG") },
            { "فرنسا", ("CDG", "LFPG") },
            { "UK", ("LHR", "EGLL") },
            { "United Kingdom", ("LHR", "EGLL") },
            { "بريطانيا", ("LHR", "EGLL") }
        };

        public FlightService(HttpClient httpClient, ILogger<FlightService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _aeroApiKey = configuration["FlightApiSettings:AeroApiKey"] ?? throw new ArgumentNullException("AeroApiKey");
            _airlabsApiKey = configuration["FlightApiSettings:AirLabsApiKey"] ?? throw new ArgumentNullException("AirLabsApiKey");
        }

        // ============================================================
        // Resolve city name to IATA code using AirLabs Suggest API
        // ============================================================
        public async Task<(string Iata, string Icao)> GetAirportCodesAsync(string cityName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cityName))
                {
                    _logger.LogWarning("Empty city name provided for IATA code lookup");
                    throw new Exception("City name cannot be empty");
                }

                cityName = cityName.Trim();
                if (cityName.Contains(","))
                {
                    cityName = cityName.Split(',')[0].Trim();
                }

                if (CountryToCityIataFallback.TryGetValue(cityName, out var fallback))
                {
                    _logger.LogInformation("IATA code resolved from fallback map. City: {CityName}, IATA: {IATA}", cityName, fallback.Iata);
                    return fallback;
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
                        iataNode.ValueKind != JsonValueKind.Null &&
                        airport.TryGetProperty("icao_code", out var icaoNode) &&
                        icaoNode.ValueKind != JsonValueKind.Null)
                    {
                        var iata = iataNode.GetString();
                        var icao = icaoNode.GetString();
                        if (!string.IsNullOrWhiteSpace(iata) && !string.IsNullOrWhiteSpace(icao))
                        {
                            _logger.LogInformation("Codes resolved successfully. City: {CityName}, IATA: {IATA}, ICAO: {ICAO}", cityName, iata.ToUpper(), icao.ToUpper());
                            return (iata.ToUpper(), icao.ToUpper());
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

                var departureCodes = await GetAirportCodesAsync(request.DepartureCity);
                await Task.Delay(1200);

                var arrivalCodes = await GetAirportCodesAsync(request.ArrivalCity);
                await Task.Delay(1200);

                var outbound = await GetFlightsAsync(departureCodes.Iata, departureCodes.Icao, arrivalCodes.Iata, request.DepartureDate);

                List<FlightDto>? returnFlights = null;

                if (request.TripType == TripType.RoundTrip)
                {
                    if (string.IsNullOrWhiteSpace(request.ReturnDate))
                    {
                        _logger.LogWarning("ReturnDate missing for RoundTrip flight search");
                        throw new Exception("ReturnDate is required for RoundTrip");
                    }

                    await Task.Delay(1200);
                    returnFlights = await GetFlightsAsync(arrivalCodes.Iata, arrivalCodes.Icao, departureCodes.Iata, request.ReturnDate);
                }

                _logger.LogInformation("Flight search completed. Outbound flights: {OutboundCount}, Return flights: {ReturnCount}", 
                    outbound.Count, returnFlights?.Count ?? 0);

                return new FlightSearchResult
                {
                    OutboundFlights = outbound,
                    ReturnFlights = returnFlights,
                    DepartureIata = departureCodes.Iata,
                    ArrivalIata = arrivalCodes.Iata
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
     string departureIcao,
     string arrivalIata,
     string date)
        {
            try
            {
                _logger.LogInformation("Fetching flights for route. From: {DepartureIata} ({DepartureIcao}), To: {ArrivalIata}, Date: {Date}", 
                    departureIata, departureIcao, arrivalIata, date);

                var morning = await FetchFromApi(
                    departureIcao,
                    arrivalIata,
                    departureIata,
                    $"{date}T00:00",
                    $"{date}T11:00");

                await Task.Delay(1200);

                var evening = await FetchFromApi(
                    departureIcao,
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
                            AirlineCode = GetString(item, "airline", "iata"),
                            DepartureAirport = departureIata,
                            ArrivalAirport = arrIata,
                            DepartureTime = GetString(item, "departure", "scheduledTime", "local"),
                            ArrivalTime = GetString(item, "arrival", "scheduledTime", "local"),
                            Status = GetString(item, "status"),
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