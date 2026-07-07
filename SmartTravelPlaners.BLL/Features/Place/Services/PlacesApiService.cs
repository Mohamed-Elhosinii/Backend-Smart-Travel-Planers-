using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Settings;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;
using SmartTravelPlaners.BLL.Features.Place.Mappers;
using SmartTravelPlaners.BLL.Features.Place.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartTravelPlaners.BLL.Features.Place.Services
{
    public class PlacesApiService : IPlacesApiService
    {
        private readonly HttpClient _foursquareHttp;
        private readonly HttpClient _serperHttp;
        private readonly FoursquareSettings _foursquareSettings;
        private readonly SerperSettings _serperSettings;
        private readonly ILogger<PlacesApiService> _logger;

        public PlacesApiService(
            IHttpClientFactory factory,
            IOptions<FoursquareSettings> foursquareSettings,
            IOptions<SerperSettings> serperSettings,
            ILogger<PlacesApiService> logger)
        {
            _foursquareSettings = foursquareSettings.Value;
            _serperSettings = serperSettings.Value;
            _foursquareHttp = factory.CreateClient("Foursquare");
            _serperHttp = factory.CreateClient("Serper");
            _logger = logger;
        }

        // ================= SEARCH =================
        public async Task<List<PlaceDto>> SearchAsync(

            string city,
            string? query = null,
            int limit = 20)
        {
            try
            {
                _logger.LogInformation("Searching places. City: {City}, Query: {Query}, Limit: {Limit}", city, query ?? "none", limit);

                var url = $"/places/search?near={Uri.EscapeDataString(city)}&limit={limit}&sort=POPULARITY";

                string categoryIds = "";
                string? textQuery = query;

                if (!string.IsNullOrEmpty(query))
                {
                    var q = query.ToLower();
                    switch (q)
                    {
                        case "attraction": categoryIds = "10000,16000"; textQuery = null; break;
                        case "restaurant": categoryIds = "13065"; textQuery = null; break;
                        case "cafe": categoryIds = "13032"; textQuery = null; break;
                        case "museum": categoryIds = "10027"; textQuery = null; break;
                        case "park": categoryIds = "16032"; textQuery = null; break;
                        case "shopping": categoryIds = "17000"; textQuery = null; break;
                        default: categoryIds = "10000,13000,16000,17000,18000"; break; // Excludes pharmacies/stations
                    }
                }

                if (!string.IsNullOrEmpty(categoryIds))
                    url += $"&categories={categoryIds}";

                if (!string.IsNullOrEmpty(textQuery))
                    url += $"&query={Uri.EscapeDataString(textQuery)}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _foursquareSettings.ServiceKey);

                request.Headers.Add("X-Places-Api-Version", _foursquareSettings.PlacesVersion);

                var response = await _foursquareHttp.SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Safely read content as string and handle empty responses.
                var contentString = response.Content == null ? null : await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(contentString))
                {
                    _logger.LogWarning("Empty response from Foursquare API for city: {City}", city);
                    return new();
                }

                var result = JsonSerializer.Deserialize<FoursquareSearchResponse>(contentString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                {
                    _logger.LogWarning("Failed to deserialize Foursquare response for city: {City}", city);
                    return new();
                }

                var places = result.results
                   .Select(p => p.ToDto())
                   .ToList();

                _logger.LogInformation("Places search completed. City: {City}, ResultCount: {ResultCount}", city, places.Count);
                return places;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search places for city: {City}. Error: {ErrorMessage}", city, ex.Message);
                throw;
            }
        }

        // ================= SEARCH BY COORDS =================
        public async Task<List<PlaceDto>> SearchByCoordsAsync(double lat, double lng, string? query = null, int limit = 20)
        {
            try
            {
                _logger.LogInformation("Searching places by coords. Lat: {Lat}, Lng: {Lng}, Query: {Query}, Limit: {Limit}", lat, lng, query ?? "none", limit);

                var url = $"/places/search?ll={lat},{lng}&radius=20000&limit={limit}&sort=POPULARITY";

                string categoryIds = "";
                string? textQuery = query;

                if (!string.IsNullOrEmpty(query))
                {
                    var q = query.ToLower();
                    switch (q)
                    {
                        case "attraction": categoryIds = "10000,16000"; textQuery = null; break;
                        case "restaurant": categoryIds = "13065"; textQuery = null; break;
                        case "cafe": categoryIds = "13032"; textQuery = null; break;
                        case "museum": categoryIds = "10027"; textQuery = null; break;
                        case "park": categoryIds = "16032"; textQuery = null; break;
                        case "shopping": categoryIds = "17000"; textQuery = null; break;
                        default: categoryIds = "10000,13000,16000,17000,18000"; break; // Excludes pharmacies/stations
                    }
                }

                if (!string.IsNullOrEmpty(categoryIds))
                    url += $"&categories={categoryIds}";

                if (!string.IsNullOrEmpty(textQuery))
                    url += $"&query={Uri.EscapeDataString(textQuery)}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _foursquareSettings.ServiceKey);

                request.Headers.Add("X-Places-Api-Version", _foursquareSettings.PlacesVersion);

                var response = await _foursquareHttp.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var contentString = response.Content == null ? null : await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(contentString))
                {
                    _logger.LogWarning("Empty response from Foursquare API for coords: {Lat},{Lng}", lat, lng);
                    return new();
                }

                var result = JsonSerializer.Deserialize<FoursquareSearchResponse>(contentString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                {
                    _logger.LogWarning("Failed to deserialize Foursquare response for coords: {Lat},{Lng}", lat, lng);
                    return new();
                }

                var places = result.results
                   .Select(p => p.ToDto())
                   .ToList();

                _logger.LogInformation("Places search by coords completed. ResultCount: {ResultCount}", places.Count);
                return places;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search places for coords: {Lat},{Lng}. Error: {ErrorMessage}", lat, lng, ex.Message);
                throw;
            }
        }

        // ================= IMAGES =================
        public async Task<List<PlacePhotoDto>> GetImages(string placeName, string category, string? address)
        {
            try
            {
                _logger.LogInformation("Fetching images for place. Name: {PlaceName}, Category: {Category}", placeName, category);

                var query = $"{placeName} {category}";

                var request = new HttpRequestMessage(HttpMethod.Post, "/images");

                request.Headers.Add("X-API-KEY", _serperSettings.ApiKey);

                request.Content = JsonContent.Create(new
                {
                    q = query,
                    num = 10
                });

                var response = await _serperHttp.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Serper API failed to fetch images for place: {PlaceName}", placeName);
                    return new List<PlacePhotoDto>();
                }

                var result = await response.Content.ReadFromJsonAsync<SerperResponse>(
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                var photos = result?.Images?
                    .Where(x => !string.IsNullOrEmpty(x.ImageUrl) && IsValidImageUrl(x.ImageUrl))
                    .Take(3)
                    .Select(x => new PlacePhotoDto
                    {
                        Urls = new List<string> { x.ImageUrl }
                    })
                    .ToList() ?? new List<PlacePhotoDto>();

                _logger.LogInformation("Images fetched successfully for place: {PlaceName}, Count: {PhotoCount}", placeName, photos.Count);
                return photos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch images for place: {PlaceName}. Error: {ErrorMessage}", placeName, ex.Message);
                throw;
            }
        }

        // ================= VALIDATION =================
        private bool IsValidImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            url = url.ToLower();

            if (url.Contains("instagram.com") ||
                url.Contains("facebook.com") ||
                url.Contains("tiktok.com") ||
                url.Contains("pinterest.com"))
                return false;

            return url.EndsWith(".jpg") ||
                   url.EndsWith(".jpeg") ||
                   url.EndsWith(".png") ||
                   url.EndsWith(".webp");
        }

        // ================= DETAILS =================
        public async Task<PlaceDetailsDto?> GetPlaceDetailsAsync(string fsqPlaceId)
        {
            try
            {
                _logger.LogInformation("Retrieving place details. PlaceId: {PlaceId}", fsqPlaceId);

                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/places/{fsqPlaceId}");

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _foursquareSettings.ServiceKey);

                request.Headers.Add("X-Places-Api-Version", _foursquareSettings.PlacesVersion);

                var response = await _foursquareHttp.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<FoursquarePlaceDetailsResponse>();

                if (result == null)
                {
                    _logger.LogWarning("Place details not found or failed to deserialize. PlaceId: {PlaceId}", fsqPlaceId);
                    return null;
                }

                _logger.LogInformation("Place details retrieved successfully. PlaceId: {PlaceId}", fsqPlaceId);
                return result.ToDetailsDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve place details. PlaceId: {PlaceId}. Error: {ErrorMessage}", fsqPlaceId, ex.Message);
                throw;
            }
        }

        // ================= NEARBY =================
        public async Task<List<NearbyPlaceDto>> GetNearbyPlacesAsync(double latitude, double longitude)
        {
            try
            {
                _logger.LogInformation("Fetching nearby places. Latitude: {Latitude}, Longitude: {Longitude}", latitude, longitude);

                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/geotagging/candidates?ll={latitude},{longitude}");

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _foursquareSettings.ServiceKey);

                request.Headers.Add("X-Places-Api-Version", _foursquareSettings.PlacesVersion);

                var response = await _foursquareHttp.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<GeotaggingResponse>();

                if (result == null)
                {
                    _logger.LogWarning("No nearby places found or failed to deserialize response. Lat: {Latitude}, Long: {Longitude}", latitude, longitude);
                    return new();
                }

                var places = result.Candidates
                   .Select(x => x.ToNearbyDto())
                    .ToList();

                _logger.LogInformation("Nearby places retrieved successfully. Count: {PlaceCount}, Latitude: {Latitude}, Longitude: {Longitude}", 
                    places.Count, latitude, longitude);

                return places;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch nearby places. Latitude: {Latitude}, Longitude: {Longitude}. Error: {ErrorMessage}", 
                    latitude, longitude, ex.Message);
                throw;
            }
        }
    }
}