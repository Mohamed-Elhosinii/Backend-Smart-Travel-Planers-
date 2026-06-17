namespace SmartTravelPlaners.BLL.Features.Flight.DTOs
{
    public enum TripType
    {
        OneWay,
        RoundTrip
    }

    public class FlightSearchRequest
    {
        // Departure city or airport name e.g. "Cairo" or "Dubai"
        public string DepartureCity { get; set; } = "";

        // Arrival city or airport name e.g. "Dubai" or "London"
        public string ArrivalCity { get; set; } = "";

        // Departure date in yyyy-MM-dd format
        public string DepartureDate { get; set; } = "";

        // Return date in yyyy-MM-dd format — required only for RoundTrip
        public string? ReturnDate { get; set; }

        public TripType TripType { get; set; } = TripType.OneWay;
    }

    public class FlightSearchResult
    {
        public List<FlightDto> OutboundFlights { get; set; } = new();
        public List<FlightDto>? ReturnFlights { get; set; }
        public bool IsRoundTrip => ReturnFlights != null;

        // Resolved airport codes from city names
        public string DepartureIata { get; set; } = "";
        public string ArrivalIata { get; set; } = "";
    }
}