namespace SmartTravelPlaners.DAL.Entities
{
    public class TripDay
    {
        public Guid Id { get; set; }

        // FK → Trip
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public int DayNumber { get; set; }         
        public DateOnly Date { get; set; }
        public decimal BudgetAllocated { get; set; }
        public decimal BudgetSpent { get; set; }

        // Navigation
        public ICollection<Activity> Activities { get; set; } = [];
    }
}
