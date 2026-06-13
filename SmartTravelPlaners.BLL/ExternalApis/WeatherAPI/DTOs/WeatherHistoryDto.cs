using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs
{
    public class WeatherHistoryDto
    {
        [JsonPropertyName("location")]
        public LocationInfo Location { get; set; } = new();

        [JsonPropertyName("forecast")]
        public HistoryForecastData Forecast { get; set; } = new();
    }
}