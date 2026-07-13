using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    public class TripHotelDto
    {
        public string Name { get; set; } = string.Empty;
        public double? PricePerNight { get; set; }
        public double? Rating { get; set; }
        public int Stars { get; set; }
        public string? Address { get; set; }
        public List<string> Images { get; set; } = new();
    }
}
