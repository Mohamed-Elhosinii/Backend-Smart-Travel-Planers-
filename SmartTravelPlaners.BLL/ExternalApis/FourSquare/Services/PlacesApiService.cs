using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.ExternalApis.Foursquare.DTOs;
using SmartTravelPlaners.BLL.ExternalApis.FourSquare.Interfaces;
using SmartTravelPlaners.BLL.ExternalApis.Foursquare.Settings;
using System.Net.Http.Headers;
using SmartTravelPlaners.BLL.ExternalApis.FourSquare.Models;
using System.Net.Http.Json;

namespace SmartTravelPlaners.BLL.ExternalApis.Foursquare.Services
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

            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ServiceKey);

            request.Headers.Add("X-Places-Api-Version", _settings.PlacesVersion);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FoursquareSearchResponse>();

            if (result is null) return new();

            return result.results.Select(p => new PlaceDto
            {
                FsqPlaceId = p.Fsq_Place_Id,
                Name = p.Name,
                Category = p.Categories.FirstOrDefault()?.Name ?? "General",
                Address = p.Location?.Formatted_Address ?? "",
                Latitude = p.Latitude,
                Longitude = p.Longitude
              
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
                Address = result.Location?.Formatted_Address ?? ""

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

            return result.Candidates.Select(x => new NearbyPlaceDto
            {
                FsqPlaceId = x.Fsq_Place_Id,
                Name = x.Name,
                Latitude = x.Latitude,
                Longitude = x.Longitude
            }).ToList();
        }
    }
}