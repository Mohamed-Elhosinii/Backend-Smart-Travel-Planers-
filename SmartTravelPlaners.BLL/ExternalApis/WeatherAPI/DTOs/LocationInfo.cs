using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs
{
    public class LocationInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("localtime")]
        public string LocalTime { get; set; } = string.Empty;
    }
}