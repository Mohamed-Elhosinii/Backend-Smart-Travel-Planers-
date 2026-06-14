using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Interfaces;

namespace SmartTravelPlaners.BLL.AI.Agents.WeatherAgent.Plugins
{
    public class WeatherPlugin
    {
        private readonly IWeatherApiService _weatherApiService;

        public WeatherPlugin(IWeatherApiService weatherApiService)
        {
            _weatherApiService = weatherApiService;
        }

        [KernelFunction, Description("Retrieves the weather forecast timeline for a given city during specific trip dates. It returns max/min temperature, humidity, precipitation probability, general conditions, and weather icon URLs.")]
        public async Task<object> GetWeatherTimeline(
            [Description("The name of the destination city, e.g., Cairo, Rome, Paris")] string cityName,
            [Description("The start date of the trip (Format: YYYY-MM-DD)")] string startDate,
            [Description("The end date of the trip (Format: YYYY-MM-DD)")] string endDate)
        {
            if (!DateTime.TryParse(startDate, out DateTime parsedStart) ||
                !DateTime.TryParse(endDate, out DateTime parsedEnd))
            {
                return "Error: Invalid date format provided to the weather plugin. Please use YYYY-MM-DD.";
            }

            var weatherData = await _weatherApiService.GetWeatherForTripAsync(cityName, parsedStart, parsedEnd);
            return weatherData;
        }
    }
}