using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.DAL.Entities
{
    public class Trip
    {
        public Guid Id { get; set; }

        // FK → UserProfile
        public Guid UserId { get; set; }
        public UserProfile User { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string? OriginCity { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int NumTravelers { get; set; }
        public decimal BudgetTotal { get; set; }
        public decimal BudgetSpent { get; set; }
        public decimal ConfirmedCost { get; set; }
        public TripStatus Status { get; set; } = TripStatus.Draft;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<TripDay> Days { get; set; } = new HashSet<TripDay>();
        public ICollection<Hotel> Hotels { get; set; } = new HashSet<Hotel>();
        public ICollection<Flight> Flights { get; set; } = new HashSet<Flight>();
        public ICollection<WeatherDay> WeatherDays { get; set; } = new HashSet<WeatherDay>();
        public ICollection<TripPreference> Preferences { get; set; } = new HashSet<TripPreference>();
        public ChatSession? ChatSession { get; set; }
    }
}
