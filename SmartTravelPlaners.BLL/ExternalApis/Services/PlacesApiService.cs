// BLL/ExternalApis/Services/PlacesApiService.cs
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.ExternalApis.DTOs;
using SmartTravelPlaners.BLL.ExternalApis.Interfaces;
using SmartTravelPlaners.BLL.ExternalApis.Settings;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SmartTravelPlaners.BLL.ExternalApis.Services
{
    public class PlacesApiService : IPlacesApiService
    {
        private readonly HttpClient _http;
        private readonly FoursquareSettings _settings;

        public PlacesApiService(HttpClient http, IOptions<FoursquareSettings> settings)
        {
            _settings = settings.Value;
            _http = http;
            _http.BaseAddress = new Uri(_settings.PlacesBaseUrl);
        }

        public async Task<List<PlaceDto>> SearchAsync(string city, string? query = null, int limit = 10)
        {
            var url = $"/places/search?near={Uri.EscapeDataString(city)}&limit={limit}";

            if (!string.IsNullOrEmpty(query))
                url += $"&query={Uri.EscapeDataString(query)}";

            // بدل GetFromJsonAsync، بنعمل Request يدوي عشان نتحكم في الـ Headers
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ServiceKey);

            request.Headers.Add("X-Places-Api-Version", _settings.PlacesVersion);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FoursquareSearchResponse>();

            if (result is null) return new();

            return result.Results.Select(p => new PlaceDto
            {
                FsqPlaceId = p.Fsq_Place_Id,
                Name = p.Name,
                Category = p.Categories.FirstOrDefault()?.Name ?? "General",
                Address = p.Location?.Formatted_Address ?? "",
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Rating = p.Rating,
                Popularity = p.Popularity,
                Price = p.Price,
                Website = p.Website
            }).ToList();
        }

        //palces details
        public async Task<PlaceDetailsDto?> GetPlaceDetailsAsync(string fsqPlaceId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/places/{fsqPlaceId}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _settings.ServiceKey);

            request.Headers.Add(
                "X-Places-Api-Version",
                _settings.PlacesVersion);

            var response = await _http.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<FoursquarePlaceDetailsResponse>();

            if (result == null)
                return null;

            return new PlaceDetailsDto
            {
                FsqPlaceId = result.Fsq_Place_Id,
                Name = result.Name,
                Address = result.Location?.Formatted_Address ?? "",
                Website = result.Website,
                Description = result.Description,
                Rating = result.Rating,
                Price = result.Price,
                Features = result.Attributes?
                    .Keys
                    .ToList() ?? new()
            };
        }

        // places photes 
        public async Task<List<PlacePhotoDto>> GetPlacePhotosAsync(string fsqPlaceId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/places/{fsqPlaceId}/photos");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _settings.ServiceKey);

            request.Headers.Add(
                "X-Places-Api-Version",
                _settings.PlacesVersion);

            var response = await _http.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var photos =
                await response.Content.ReadFromJsonAsync<List<FoursquarePhoto>>();

            if (photos == null)
                return new();

            return photos.Select(x => new PlacePhotoDto
            {
                Url = $"{x.Prefix}original{x.Suffix}"
            }).ToList();
        }

        //places tips 
        public async Task<List<PlaceTipDto>> GetPlaceTipsAsync(string fsqPlaceId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/places/{fsqPlaceId}/tips");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _settings.ServiceKey);

            request.Headers.Add(
                "X-Places-Api-Version",
                _settings.PlacesVersion);

            var response = await _http.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var tips =
                await response.Content.ReadFromJsonAsync<List<FoursquareTip>>();

            if (tips == null)
                return new();

            return tips.Select(x => new PlaceTipDto
            {
                Text = x.Text
            }).ToList();
        }

        //near places 

        public async Task<List<NearbyPlaceDto>>GetNearbyPlacesAsync( double latitude, double longitude)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/geotagging/candidates?ll={latitude},{longitude}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _settings.ServiceKey);

            request.Headers.Add(
                "X-Places-Api-Version",
                _settings.PlacesVersion);

            var response = await _http.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<GeotaggingResponse>();

            if (result == null)
                return new();

            return result.Results.Select(x => new NearbyPlaceDto
            {
                FsqPlaceId = x.Fsq_Place_Id,
                Name = x.Name,
                Latitude = x.Latitude,
                Longitude = x.Longitude
            }).ToList();
        }
    }
}