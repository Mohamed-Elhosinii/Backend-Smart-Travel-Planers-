using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.DAL.Entities
{
    public class Flight
    {
        public Guid Id { get; set; }

        // FK → Trip
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public string? Airline { get; set; }
        public string? FlightNumber { get; set; }
        public string Origin { get; set; } = string.Empty;        
        public string Destination { get; set; } = string.Empty;    
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }
        public string? BookingUrl { get; set; }
        public CabinClass CabinClass { get; set; } = CabinClass.Economy;
        public BookingStatus Status { get; set; } = BookingStatus.Suggested;

        // UI Detail Fields
        public string? AirlineCode { get; set; }
        public string? DepartureTerminal { get; set; }
        public string? ArrivalTerminal { get; set; }
    }
}
