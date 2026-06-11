using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.DTOs
{
    public class HotelLocation
    {
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }
}

