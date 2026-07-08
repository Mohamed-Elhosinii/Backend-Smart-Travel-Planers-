using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Weather.DTOs
{
    public class VisualCrossingResponseDto
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("resolvedAddress")]
        public string ResolvedAddress { get; set; } = string.Empty; 

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("days")]
        public List<VisualCrossingDayItem> Days { get; set; } = new();
    }
}