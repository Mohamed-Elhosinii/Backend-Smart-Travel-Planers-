namespace SmartTravelPlaners.DAL.Entities
{
    public class UserProfile
    {
        public Guid Id { get; set; }

        // FK → AspNetUsers
        public string AspNetUserId { get; set; } = string.Empty;
        public ApplicationUser AspNetUser { get; set; } = null!;

        public string? PreferredCurrency { get; set; }  
        public string? AvatarUrl { get; set; }
        public string? Country { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Trip> Trips { get; set; } = new HashSet<Trip>();
        public ICollection<Subscription> Subscriptions { get; set; } = new HashSet<Subscription>();
        public ICollection<UsageCounter> UsageCounters { get; set; } = new HashSet<UsageCounter>();
    }
}
