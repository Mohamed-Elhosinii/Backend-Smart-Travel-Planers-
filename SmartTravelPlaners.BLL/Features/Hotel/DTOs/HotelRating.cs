using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Hotel.DTOs
{
    public class HotelRating
    {
        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("votes")]
        public int? Votes { get; set; }

        [JsonPropertyName("rating_max")]
        public double? RatingMax { get; set; }
    }
}

