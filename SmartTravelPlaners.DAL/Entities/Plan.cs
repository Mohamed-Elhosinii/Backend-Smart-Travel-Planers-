namespace SmartTravelPlaners.DAL.Entities
{
    public class Plan
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PriceMonthly { get; set; }

        /// <summary>Null means unlimited.</summary>
        public int? MaxTripsPerMonth { get; set; }

        /// <summary>Null means unlimited.</summary>
        public int? MaxMessagesPerMonth { get; set; }

        /// <summary>Reserved for future Paymob recurring-plan integration.</summary>
        public string? PaymobPlanId { get; set; }

        // Navigation
        public ICollection<Subscription> Subscriptions { get; set; } = new HashSet<Subscription>();
    }
}
