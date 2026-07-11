using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Place.DTOs
{
    public class NearbyPlaceDto
    {
        public string FsqPlaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Address { get; set; }
        public string? Category { get; set; }
        public double Rating { get; set; }
        public string? ImageUrl { get; set; }
        public int PriceLevel { get; set; }
        public List<PlacePhotoDto> Images { get; set; } = new List<PlacePhotoDto>();
    }
   
}