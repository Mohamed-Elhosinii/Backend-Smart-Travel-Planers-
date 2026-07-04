namespace SmartTravelPlaners.BLL.Features.Subscription.DTOs
{
    public class PlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PriceMonthly { get; set; }
        public int? MaxTripsPerMonth { get; set; }
        public int? MaxMessagesPerMonth { get; set; }
    }
}
