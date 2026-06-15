using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Chat.DTOs
{
    public class TripCreateDto
    {
        public string Destination { get; set; } = string.Empty;
        public string OriginCity { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int NumTravelers { get; set; }
        public decimal BudgetTotal { get; set; }
        public List<string> Preferences { get; set; } = new();
    }
}
