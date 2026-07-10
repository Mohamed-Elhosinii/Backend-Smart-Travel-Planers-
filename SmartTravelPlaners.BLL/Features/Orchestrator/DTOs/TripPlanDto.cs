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
        public decimal BudgetSpent { get; set; }
        public decimal ConfirmedCost { get; set; }
        public decimal EstimatedTotalCost { get; set; }

        public TripHotelDto? Hotel { get; set; }
        public TripFlightDto? Flight { get; set; }
        public List<DayPlanDto> Days { get; set; } = new();

        /// <summary>Daily weather forecast for the destination across the trip window.</summary>
        public List<DayWeatherDto> Weather { get; set; } = new();

        /// <summary>Budget validation warnings</summary>
        public List<BudgetWarning> BudgetWarnings { get; set; } = new();

        /// <summary>Detailed budget breakdown by category</summary>
        public BudgetBreakdownDto? BudgetBreakdown { get; set; }

        public string Summary { get; set; } = string.Empty;
    }
}
