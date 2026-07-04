namespace SmartTravelPlaners.BLL.Features.Subscription.DTOs
{
    public class SubscriptionDto
    {
        public string PlanName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CurrentPeriodEnd { get; set; }
        public int TripsUsed { get; set; }
        public int? TripsLimit { get; set; }
        public int MessagesUsed { get; set; }
        public int? MessagesLimit { get; set; }
    }
}
