// BLL/ExternalApis/Services/PlacesApiService.cs
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.ExternalApis.DTOs.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Interfaces.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Models.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Settings.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Settings.Places;
using SmartTravelPlaners.BLL.Mappers.Foursquare;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SmartTravelPlaners.BLL.ExternalApis.Services.Foursquare
{
    public class PlacesApiService : IPlacesApiService
    {
        private readonly HttpClient _foursquareHttp;
        private readonly HttpClient _serperHttp;
        private readonly FoursquareSettings _foursquareSettings;
        private readonly SerperSettings _serperSettings;

        public PlacesApiService(IHttpClientFactory factory, IOptions<FoursquareSettings> FoursquareSettings, IOptions<SerperSettings> serperSettings)
        {
            _foursquareSettings = FoursquareSettings.Value;
            _serperSettings  = serperSettings.Value;
            _foursquareHttp = factory.CreateClient("Foursquare");
            _serperHttp = factory.CreateClient("Serper");
        }

        //places with image search
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
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _foursquareSettings.ServiceKey);

            request.Headers.Add("X-Places-Api-Version", _foursquareSettings.PlacesVersion);

            var response = await _foursquareHttp.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FoursquareSearchResponse>();

            if (result is null || result.results is null)
                return new List<PlaceDto>();

            var places = result.results
    .Select(p => p.ToDto())
    .ToList();

            foreach (var place in places)
            {
                var images = await GetImages(place.Name, place.Category, place.Address);
                if (images == null || !images.Any())
                {
                    images = new List<PlacePhotoDto>();
                }

                place.Images = images;

                
            }
            return places;
        }
 
        // places photos 

        public async Task<List<PlacePhotoDto>> GetImages(string placeName, string category, string? address)
        {
            var query = $"{placeName} {category}";

            var url = "/images";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

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

            var images = result?.Images?
                .Where(x => !string.IsNullOrEmpty(x.ImageUrl) && IsValidImageUrl(x.ImageUrl))
                .Take(3)
                .Select(x => new PlacePhotoDto
                {
                    Urls = new List<string> { x.ImageUrl }
                })
                .ToList() ?? new List<PlacePhotoDto>();

            return images;
        }

        //IsValidImageUrl function to filter out non-image URLs and social media links
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
        //palces details
        public async Task<PlaceDetailsDto?> GetPlaceDetailsAsync(string fsqPlaceId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/places/{fsqPlaceId}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _foursquareSettings.ServiceKey);

            request.Headers.Add(
                "X-Places-Api-Version",
                _foursquareSettings.PlacesVersion);

            var response = await _foursquareHttp.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<FoursquarePlaceDetailsResponse>();



            if (result == null)
                return new PlaceDetailsDto();

            var place= result.ToDetailsDto();
            var images = await GetImages(place.Name, place.Category, place.Address);

            if (images == null || !images.Any())
            {
                images = new List<PlacePhotoDto>(); 
            }

            place.Images = images;

            return place;
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
                    _foursquareSettings.ServiceKey);

            request.Headers.Add(
                "X-Places-Api-Version",
                _foursquareSettings.PlacesVersion);

            var response = await _foursquareHttp.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<GeotaggingResponse>();

            if (result == null)
                return new List<NearbyPlaceDto>();

            var nearbyPlaces = result.Candidates
                .Select(x => x.ToNearbyDto())
                .ToList();
          

            foreach (var place in nearbyPlaces)
            {
                var images = await GetImages(place.Name, place.Category, place.Address);
                if (images == null || !images.Any())
                {
                    images = new List<PlacePhotoDto>();
                }

                place.Images = images;


            }
            return nearbyPlaces;
        }
       
        
    }
}