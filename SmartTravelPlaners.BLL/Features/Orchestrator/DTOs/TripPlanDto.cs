using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    public class TripPlanDto
    {
        public Guid TripId { get; set; }
        public string Destination { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal BudgetTotal { get; set; }
        public decimal EstimatedTotalCost { get; set; }

        public TripHotelDto? Hotel { get; set; }
        public TripFlightDto? Flight { get; set; }
        public List<DayPlanDto> Days { get; set; } = new();

        /// <summary>Daily weather forecast for the destination across the trip window.</summary>
        public List<DayWeatherDto> Weather { get; set; } = new();

        public string Summary { get; set; } = string.Empty;
    }
}
