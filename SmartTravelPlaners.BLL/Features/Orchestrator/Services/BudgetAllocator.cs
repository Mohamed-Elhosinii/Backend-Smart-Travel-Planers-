namespace SmartTravelPlaners.BLL.Features.Orchestrator.Services
{
    public static class BudgetAllocator
    {
        // Simple fixed-split heuristic for v1
        private const decimal HotelShare = 0.40m;
        private const decimal FlightShare = 0.35m;
        private const decimal ActivitiesShare = 0.25m;

        public static decimal HotelBudget(decimal totalBudget) => totalBudget * HotelShare;
        public static decimal FlightBudget(decimal totalBudget) => totalBudget * FlightShare;
        public static decimal ActivitiesBudget(decimal totalBudget) => totalBudget * ActivitiesShare;

        public static decimal HotelBudgetPerNight(decimal totalBudget, int numberOfNights)
            => numberOfNights <= 0 ? HotelBudget(totalBudget) : HotelBudget(totalBudget) / numberOfNights;

        public static decimal DailyActivityBudget(decimal totalBudget, int numberOfDays)
            => numberOfDays <= 0 ? ActivitiesBudget(totalBudget) : ActivitiesBudget(totalBudget) / numberOfDays;

        // If no flight is needed (no origin city), redistribute its share to hotel + activities
        public static (decimal hotelBudget, decimal activitiesBudget) WithoutFlight(decimal totalBudget)
        {
            var extra = FlightShare;
            var newHotelShare = HotelShare + extra * 0.6m;
            var newActivitiesShare = ActivitiesShare + extra * 0.4m;
            return (totalBudget * newHotelShare, totalBudget * newActivitiesShare);
        }
    }
}