namespace SmartTravelPlaners.BLL.ExternalApis.DTOs
{
    public class FlightDto
    {
        public string FlightNumber { get; set; } = "";
        public string AirlineName { get; set; } = "";
        public string DepartureAirport { get; set; } = "";
        public string ArrivalAirport { get; set; } = "";
        public string DepartureTime { get; set; } = "";
        public string ArrivalTime { get; set; } = "";
        public string Status { get; set; } = "";
        public string AircraftModel { get; set; } = "";
        public string DepartureTerminal { get; set; } = "";
        public string ArrivalTerminal { get; set; } = "";
    }
}