using Microsoft.Extensions.Configuration;
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


