namespace SmartTravelPlaners.DAL.Entities
{
    public class UsageCounter
    {
        public Guid Id { get; set; }

        // FK → UserProfile
        public Guid UserProfileId { get; set; }
        public UserProfile UserProfile { get; set; } = null!;

        /// <summary>Format: "yyyy-MM" (e.g. "2026-06").</summary>
        public string PeriodMonth { get; set; } = string.Empty;

        public int TripsUsed { get; set; }
        public int MessagesUsed { get; set; }
    }
}
