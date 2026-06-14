using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs
{
    public class ForecastData
    {
        [JsonPropertyName("forecastday")]
        public List<ForecastDayItem> ForecastDay { get; set; } = new();
    }
}