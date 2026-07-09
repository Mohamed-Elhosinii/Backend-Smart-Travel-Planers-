using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SmartTravelPlaners.BLL.Features.Weather.DTOs;
using SmartTravelPlaners.BLL.Features.Weather.Interfaces;
using SmartTravelPlaners.BLL.Features.Weather.Settings;
using SmartTravelPlaners.BLL.Features.Weather.Plugins;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SmartTravelPlaners.BLL.Features.Weather.Services
{

    public class WeatherApiService : IWeatherApiService
    {
        private readonly HttpClient _httpClient;
        private readonly WeatherApiSettings _settings;
        private readonly ILogger<WeatherApiService> _logger;

        public WeatherApiService(HttpClient httpClient, IOptions<WeatherApiSettings> settings, ILogger<WeatherApiService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<object> GetWeatherForTripAsync(string cityName, DateTime startDate, DateTime endDate)
        {
            try
            {
                if (!string.IsNullOrEmpty(cityName))
                {
                    cityName = cityName.Trim();
                    if (cityName.Contains(","))
                    {
                        cityName = cityName.Split(',')[0].Trim();
                    }
                }

                _logger.LogInformation("Fetching weather forecast. City: {CityName}, StartDate: {StartDate}, EndDate: {EndDate}", 
                    cityName, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

                string start = startDate.ToString("yyyy-MM-dd");
                string end = endDate.ToString("yyyy-MM-dd");

                var url = $"{_settings.BaseUrl}timeline/{cityName}/{start}/{end}?unitGroup=metric&key={_settings.ApiKey}&include=days&contentType=json";

                var response = await _httpClient.GetFromJsonAsync<VisualCrossingResponseDto>(url);

                if (response != null)
                {
                    _logger.LogInformation("Weather forecast retrieved successfully for city: {CityName}", cityName);
                    return response;
                }

                _logger.LogWarning("Weather API returned null response for city: {CityName}", cityName);
                return new VisualCrossingResponseDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch weather for city: {CityName}. Error: {ErrorMessage}", cityName, ex.Message);
                return new VisualCrossingResponseDto { Address = cityName };
            }
        }
    }
}


