using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs
{
    public class WeatherForecastDto
    {
        [JsonPropertyName("location")]
        public LocationInfo Location { get; set; } = new();

        [JsonPropertyName("forecast")]
        public ForecastData Forecast { get; set; } = new();
    }
}