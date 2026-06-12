namespace SmartTravelPlaners.BLL.ExternalApis.DTOs
{
    public enum TripType
    {
        OneWay,
        RoundTrip
    }

    public class FlightSearchRequest
    {
        /// <summary>Departure airport IATA code e.g. CAI</summary>
        public string DepartureAirport { get; set; } = "";

        /// <summary>Arrival airport IATA code e.g. DXB</summary>
        public string ArrivalAirport { get; set; } = "";

        /// <summary>Departure date in yyyy-MM-dd format</summary>
        public string DepartureDate { get; set; } = "";

        /// <summary>Return date in yyyy-MM-dd format — required only for RoundTrip</summary>
        public string? ReturnDate { get; set; }

        public TripType TripType { get; set; } = TripType.OneWay;
    }

    public class FlightSearchResult
    {
        public List<FlightDto> OutboundFlights { get; set; } = new();
        public List<FlightDto>? ReturnFlights { get; set; }
        public bool IsRoundTrip => ReturnFlights != null;
    }
}