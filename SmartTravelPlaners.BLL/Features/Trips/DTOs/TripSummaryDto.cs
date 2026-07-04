using System;
using System.Collections.Generic;

namespace SmartTravelPlaners.BLL.Features.Trips.DTOs
{
    public class TripSummaryDto
    {
        public Guid Id { get; set; }
        public string Destination { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string OriginCity { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? CoverImage { get; set; }
        public decimal BudgetTotal { get; set; }
        public decimal BudgetSpent { get; set; }
        public List<string> TravelStyle { get; set; } = new();
        public string Status { get; set; } = string.Empty;
    }
}
