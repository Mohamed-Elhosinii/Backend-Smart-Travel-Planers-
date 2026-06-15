using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Hotel.DTOs
{
    public class HotelPrice
    {
        [JsonPropertyName("current")]
        public double? Current { get; set; }

        [JsonPropertyName("regular")]
        public double? Regular { get; set; }

        [JsonPropertyName("max_price")]
        public double? MaxPrice { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("price_per_night")]
        public double? PricePerNight { get; set; }
    }
}

