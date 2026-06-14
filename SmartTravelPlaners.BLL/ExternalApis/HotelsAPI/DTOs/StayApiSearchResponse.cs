using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.DTOs
{
    public class StayApiSearchResponse
    {
        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("check_in")]
        public string CheckIn { get; set; } = string.Empty;

        [JsonPropertyName("check_out")]
        public string CheckOut { get; set; } = string.Empty;

        [JsonPropertyName("hotels")]
        public List<GoogleHotelDto> Hotels { get; set; } = new();

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
    }
}

