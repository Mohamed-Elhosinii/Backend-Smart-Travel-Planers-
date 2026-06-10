// BLL/ExternalApis/Services/PlacesApiService.cs
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.ExternalApis.DTOs.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Interfaces.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Models.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Settings.Foursquare;
using SmartTravelPlaners.BLL.Mappers.Foursquare;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartTravelPlaners.BLL.ExternalApis.Services.Foursquare
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
        

        public async Task<List<PlaceDto>> SearchAsync(double ?lat, double? lon, string city, string? query = null, int limit = 20)
        {
            var url = "/places/search";
            if (lat.HasValue && lon.HasValue)
            {
                url += $"?ll={lat.Value},{lon.Value}";
            }
            else
            {
                url += $"?near={Uri.EscapeDataString(city)}";
            }

            url += $"&limit={limit}";

            if (!string.IsNullOrEmpty(query))
                url += $"&query={Uri.EscapeDataString(query)}";

          
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ServiceKey);

            request.Headers.Add("X-Places-Api-Version", _settings.PlacesVersion);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FoursquareSearchResponse>();

            if (result is null || result.results is null)
                return new List<PlaceDto>();

            return result.results
                .Select(p => p.ToDto())
                .ToList();
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
                return new PlaceDetailsDto();

            return result.ToDetailsDto();
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
                return new List<NearbyPlaceDto>();

            return result.Candidates
                .Select(x => x.ToNearbyDto())
                .ToList();
        }
    }
}