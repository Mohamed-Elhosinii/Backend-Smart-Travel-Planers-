using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.DTOs;
using SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.Interfaces;
using SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.Settings;

namespace SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.Services
{
    public class HotelApiService : IHotelApiService
    {
        private readonly HttpClient _httpClient;
        private readonly HotelApiSettings _settings;

        public HotelApiService(HttpClient httpClient, IOptions<HotelApiSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
        }

        public async Task<List<GoogleHotelDto>> GetAvailableHotelsAsync(string location, string checkIn, string checkOut, int adults = 2, int children = 0)
        {
            try
            {
                var searchUrl = $"google_hotels/search?location={Uri.EscapeDataString(location)}&check_in={checkIn}&check_out={checkOut}&adults={adults}&children={children}&currency=USD";

                var responseString = await _httpClient.GetStringAsync(searchUrl);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var searchResult = JsonSerializer.Deserialize<StayApiSearchResponse>(responseString, options);

                return searchResult?.Hotels ?? new List<GoogleHotelDto>();
            }
            catch (Exception ex)
            {
                return new List<GoogleHotelDto>();
            }
        }
    }
}

