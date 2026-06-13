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
            var daysUntilTrip = (startDate.Date - DateTime.Today).TotalDays;

            if (daysUntilTrip >= 0 && daysUntilTrip <= 14)
            {
                int totalForecastDays = (int)(endDate.Date - DateTime.Today).TotalDays + 1;
                if (totalForecastDays > 14) totalForecastDays = 14;

                var url = $"{_settings.BaseUrl}forecast.json?key={_settings.ApiKey}&q={cityName}&days={totalForecastDays}&aqi=no";

                var response = await _httpClient.GetFromJsonAsync<WeatherForecastDto>(url);
                return response ?? new WeatherForecastDto();
            }
            else
            {
                var historicalStartDate = startDate.AddYears(-1).ToString("yyyy-MM-dd");
                var historicalEndDate = endDate.AddYears(-1).ToString("yyyy-MM-dd");

                var url = $"{_settings.BaseUrl}history.json?key={_settings.ApiKey}&q={cityName}&dt={historicalStartDate}&end_dt={historicalEndDate}";

                var response = await _httpClient.GetFromJsonAsync<WeatherHistoryDto>(url);
                return response ?? new WeatherHistoryDto();
            }
        }
    }
}