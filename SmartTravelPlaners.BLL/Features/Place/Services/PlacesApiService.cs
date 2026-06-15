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

        public PlacesApiService(
            IHttpClientFactory factory,
            IOptions<FoursquareSettings> foursquareSettings,
            IOptions<SerperSettings> serperSettings)
        {
            _foursquareSettings = foursquareSettings.Value;
            _serperSettings = serperSettings.Value;
            _foursquareHttp = factory.CreateClient("Foursquare");
            _serperHttp = factory.CreateClient("Serper");
        }

        // ================= SEARCH =================
        public async Task<List<PlaceDto>> SearchAsync(
            
            string city,
            string? query = null,
            int limit = 20)
        {
            var url = $"/places/search?near={Uri.EscapeDataString(city)}&limit={limit}";

            if (!string.IsNullOrEmpty(query))
                url += $"&query={Uri.EscapeDataString(query)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _foursquareSettings.ServiceKey);

            request.Headers.Add("X-Places-Api-Version", _foursquareSettings.PlacesVersion);

            var response = await _foursquareHttp.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FoursquareSearchResponse>();

            if (result == null)
                return new();

            return result.results
               .Select(p => p.ToDto())
               .ToList();

          
        }

        // ================= IMAGES =================
        public async Task<List<PlacePhotoDto>> GetImages(string placeName, string category, string? address)
        {
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
                return new List<PlacePhotoDto>();

            var result = await response.Content.ReadFromJsonAsync<SerperResponse>(
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result?.Images?
                .Where(x => !string.IsNullOrEmpty(x.ImageUrl) && IsValidImageUrl(x.ImageUrl))
                .Take(3)
                .Select(x => new PlacePhotoDto
                {
                    Urls = new List<string> { x.ImageUrl }
                })
                .ToList() ?? new List<PlacePhotoDto>();
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
                return null;

            return result.ToDetailsDto();
        }

        // ================= NEARBY =================
        public async Task<List<NearbyPlaceDto>> GetNearbyPlacesAsync(double latitude, double longitude)
        {
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
                return new();

            return result.Candidates
               .Select(x => x.ToNearbyDto())
                .ToList();

         
        }
    }
}