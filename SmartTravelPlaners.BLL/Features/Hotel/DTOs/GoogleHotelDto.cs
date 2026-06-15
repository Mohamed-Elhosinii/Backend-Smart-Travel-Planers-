using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Hotel.DTOs
{
    public class GoogleHotelDto
    {
        [JsonPropertyName("hotel_id")]
        public string HotelId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public HotelLocation Location { get; set; } = new();

        [JsonPropertyName("price")]
        public HotelPrice Price { get; set; } = new();

        [JsonPropertyName("rating")]
        public HotelRating Rating { get; set; } = new();

        [JsonPropertyName("stars")]
        public int? Stars { get; set; }

        [JsonPropertyName("check_in_time")]
        public string CheckInTime { get; set; } = string.Empty;

        [JsonPropertyName("check_out_time")]
        public string CheckOutTime { get; set; } = string.Empty;

        [JsonPropertyName("amenities")]
        public List<string> Amenities { get; set; } = new();

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("images")]
        public List<string> Images { get; set; } = new();

        [JsonPropertyName("is_paid")]
        public bool IsPaid { get; set; }
    }
}
