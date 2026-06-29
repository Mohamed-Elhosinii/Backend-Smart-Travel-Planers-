using Xunit;
using SmartTravelPlaners.BLL.Features.Orchestrator.Services;

namespace SmartTravelPlaners.Tests.Features.Orchestrator
{
    public class BudgetAllocatorTests
    {
        [Fact]
        public void HotelBudget_ShouldReturn40Percent()
        {
            var result = BudgetAllocator.HotelBudget(1000);

            Assert.Equal(400, result);
        }

        [Fact]
        public void FlightBudget_ShouldReturn35Percent()
        {
            var result = BudgetAllocator.FlightBudget(1000);

            Assert.Equal(350, result);
        }

        [Fact]
        public void ActivitiesBudget_ShouldReturn25Percent()
        {
            var result = BudgetAllocator.ActivitiesBudget(1000);

            Assert.Equal(250, result);
        }

        [Fact]
        public void HotelBudgetPerNight_ShouldDivideCorrectly()
        {
            var result = BudgetAllocator.HotelBudgetPerNight(1000, 5);

            Assert.Equal(80, result);
        }

        [Fact]
        public void DailyActivityBudget_ShouldDivideCorrectly()
        {
            var result = BudgetAllocator.DailyActivityBudget(1000, 5);

            Assert.Equal(50, result);
        }

        [Fact]
        public void WithoutFlight_ShouldRedistributeBudget()
        {
            var (hotel, activities) = BudgetAllocator.WithoutFlight(1000);

            Assert.True(hotel > 400);
            Assert.True(activities > 250);
        }
    }
}
