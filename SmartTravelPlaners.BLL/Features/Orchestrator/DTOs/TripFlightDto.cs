using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    public class TripFlightDto
    {
        public string AirlineName { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public string DepartureAirport { get; set; } = string.Empty;
        public string ArrivalAirport { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;

        /// <summary>
        /// Estimated flight cost. The schedule provider (AeroDataBox/AirLabs) returns no
        /// ticket price, so this is the budget the orchestrator allocated to flights.
        /// </summary>
        public decimal EstimatedPrice { get; set; }
    }
}
