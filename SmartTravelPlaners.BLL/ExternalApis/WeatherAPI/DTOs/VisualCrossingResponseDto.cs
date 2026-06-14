using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs
{
    public class VisualCrossingResponseDto
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("resolvedAddress")]
        public string ResolvedAddress { get; set; } = string.Empty; 

        [JsonPropertyName("days")]
        public List<VisualCrossingDayItem> Days { get; set; } = new();
    }
}