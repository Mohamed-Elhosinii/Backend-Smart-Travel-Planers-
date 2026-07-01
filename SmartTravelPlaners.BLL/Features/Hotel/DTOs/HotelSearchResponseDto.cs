using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Hotel.DTOs
{
    public class BookingLinksDto
    {
        public bool CacheHit { get; set; }
        public Dictionary<string, string> Links { get; set; } = new();
    }

    public class HotelSearchResponseDto
    {
        public bool CacheHit { get; set; }
        public List<HotelDto> Hotels { get; set; } = new();
        public MetadataDto Metadata { get; set; } = new();

        public class MetadataDto
        {
            public int TotalResults { get; set; }
            public List<string> SourcesUsed { get; set; } = new();
            public DateTime RetrievedAt { get; set; }
        }
    }

    public class HotelDto
    {
        public string HotelId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public HotelPriceDto? Price { get; set; }
        public HotelReviewDto? Rating { get; set; }
        public List<string> Images { get; set; } = new();
        public LocationDto Location { get; set; } = new();
        public List<string> Amenities { get; set; } = new();
        public int Stars { get; set; }
        public List<string> Sources { get; set; } = new();
    }

    public class HotelPriceDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public bool IsEstimated { get; set; }
    }

    public class HotelReviewDto
    {
        public double Score { get; set; }
        public int ReviewCount { get; set; }
        public double WeightedScore { get; set; }
    }

    public class LocationDto
    {
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
