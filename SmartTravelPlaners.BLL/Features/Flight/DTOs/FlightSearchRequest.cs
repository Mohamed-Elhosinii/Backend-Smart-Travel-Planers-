namespace SmartTravelPlaners.BLL.Features.Flight.DTOs
{
    public enum TripType
    {
        OneWay,
        RoundTrip
    }

    public class FlightSearchRequest
    {
        //Departure airport IATA code e.g. CAI
        public string DepartureAirport { get; set; } = "";

        //Arrival airport IATA code e.g. DXB
        public string ArrivalAirport { get; set; } = "";

        //<summary>Departure date in yyyy-MM-dd format
        public string DepartureDate { get; set; } = "";

        //Return date in yyyy-MM-dd format — required only for RoundTrip
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