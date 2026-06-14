using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Interfaces;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Settings;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Services
{
    public class WeatherApiService : IWeatherApiService
    {
        private readonly HttpClient _httpClient;
        private readonly WeatherApiSettings _settings;

        public WeatherApiService(HttpClient httpClient, IOptions<WeatherApiSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<object> GetWeatherForTripAsync(string cityName, DateTime startDate, DateTime endDate)
        {
            string start = startDate.ToString("yyyy-MM-dd");
            string end = endDate.ToString("yyyy-MM-dd");

            var url = $"{_settings.BaseUrl}timeline/{cityName}/{start}/{end}?unitGroup=metric&key={_settings.ApiKey}&include=days&contentType=json";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<VisualCrossingResponseDto>(url);
                return response ?? new VisualCrossingResponseDto();
            }
            catch (Exception)
            {
                return new VisualCrossingResponseDto { Address = cityName };
            }
        }
    }
}