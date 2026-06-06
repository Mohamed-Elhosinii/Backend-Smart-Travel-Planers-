namespace SmartTravelPlaners.DAL.Entities
{
  
    public class TripPreference
    {
        public Guid Id { get; set; }

        // FK → Trip
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public string Category { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
