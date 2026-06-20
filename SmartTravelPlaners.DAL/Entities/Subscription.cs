using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.DAL.Entities
{
    public class Subscription
    {
        public Guid Id { get; set; }

        // FK → UserProfile
        public Guid UserProfileId { get; set; }
        public UserProfile UserProfile { get; set; } = null!;

        // FK → Plan
        public Guid PlanId { get; set; }
        public Plan Plan { get; set; } = null!;

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }

        /// <summary>Reserved for future Paymob subscription tracking.</summary>
        public string? PaymobSubscriptionId { get; set; }

        // Navigation
        public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new HashSet<PaymentTransaction>();
    }
}
