namespace SmartTravelPlaners.DAL.Entities
{
    public class WeatherDay
    {
        public Guid Id { get; set; }

        // FK → Trip
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public DateOnly Date { get; set; }
        public double TempMax { get; set; }
        public double TempMin { get; set; }
        public double Humidity { get; set; }
        public double PrecipProb { get; set; }
        public string Conditions { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
    }
}
