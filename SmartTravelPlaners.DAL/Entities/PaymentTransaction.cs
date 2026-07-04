namespace SmartTravelPlaners.DAL.Entities
{
    public class PaymentTransaction
    {
        public Guid Id { get; set; }

        // FK → Subscription
        public Guid SubscriptionId { get; set; }
        public Subscription Subscription { get; set; } = null!;

        /// <summary>Created at order-creation time so the webhook can look up who paid.</summary>
        public string PaymobOrderId { get; set; } = string.Empty;

        /// <summary>Filled when the webhook fires.</summary>
        public string? PaymobTransactionId { get; set; }

        public decimal Amount { get; set; }

        /// <summary>"pending", "paid", or "failed".</summary>
        public string Status { get; set; } = "pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
