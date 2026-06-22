using SmartTravelPlaners.BLL.Features.Place.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    public class ActivityPlanDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;   // "Restaurant", "Attraction", "Cafe"...
        public string? LocationName { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public string? TimeSlot { get; set; }               // "Morning" | "Lunch" | "Afternoon" | "Dinner"
        public decimal EstimatedCost { get; set; }
        public string? PlaceId { get; set; }
        public List<PlacePhotoDto> Images { get; set; }= new List<PlacePhotoDto>();
    }
}
