using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs
{
    public class HistoryForecastData
    {
        [JsonPropertyName("forecastday")]
        public List<HistoryDayItem> ForecastDay { get; set; } = new();
    }
}