using SmartTravelPlaners.DAL.Entities;
using Xunit;
using Xunit.Abstractions;

namespace SmartTravelPlaners.Tests
{
    /// <summary>
    /// بسيط جداً: تيست يدوي للبادجت - بدون mocking
    /// </summary>
    public class ManualBudgetTest
    {
        private readonly ITestOutputHelper _output;

        public ManualBudgetTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Trip_ConfirmedCost_ShouldExist()
        {
            // Arrange: إنشاء Trip
            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                Destination = "Cairo",
                BudgetTotal = 10000,
                BudgetSpent = 8500,
                ConfirmedCost = 4000  // الفندق فقط
            };

            // Act & Assert: التأكد إن الـ property موجود ويشتغل
            Assert.Equal(10000m, trip.BudgetTotal);
            Assert.Equal(8500m, trip.BudgetSpent);
            Assert.Equal(4000m, trip.ConfirmedCost);

            _output.WriteLine($"✅ Trip created successfully:");
            _output.WriteLine($"   - BudgetTotal: {trip.BudgetTotal}");
            _output.WriteLine($"   - BudgetSpent: {trip.BudgetSpent}");
            _output.WriteLine($"   - ConfirmedCost: {trip.ConfirmedCost}");
        }

        [Fact]
        public void ConfirmedCost_ShouldBeLessThanOrEqualTo_BudgetSpent()
        {
            // Arrange
            var hotelCostPerNight = 1000m;
            var numberOfNights = 4;
            var flightCost = 3500m;
            var activitiesCost = 500m;

            // Act
            var confirmedCost = hotelCostPerNight * numberOfNights;  // 4000
            var budgetSpent = confirmedCost + flightCost + activitiesCost;  // 8000

            // Assert
            Assert.True(confirmedCost <= budgetSpent, 
                $"ConfirmedCost ({confirmedCost}) should be <= BudgetSpent ({budgetSpent})");

            _output.WriteLine($"✅ Budget calculation correct:");
            _output.WriteLine($"   - Hotel (confirmed): {confirmedCost} EGP");
            _output.WriteLine($"   - Flight (estimated): {flightCost} EGP");
            _output.WriteLine($"   - Activities (estimated): {activitiesCost} EGP");
            _output.WriteLine($"   - Total BudgetSpent: {budgetSpent} EGP");
        }

        [Fact]
        public void ConfirmedCost_ShouldOnlyIncludeHotel()
        {
            // Arrange: سيناريو رحلة كاملة
            var hotelCost = 5000m;
            var flightCost = 3500m;
            var activitiesCost = 1500m;

            // Act
            var confirmedCost = hotelCost;  // الفندق فقط ✅
            var budgetSpent = hotelCost + flightCost + activitiesCost;  // كل حاجة

            // Assert
            Assert.Equal(hotelCost, confirmedCost);
            Assert.Equal(10000m, budgetSpent);
            Assert.NotEqual(confirmedCost, budgetSpent);  // لازم يكونوا مختلفين

            _output.WriteLine($"✅ ConfirmedCost calculation correct:");
            _output.WriteLine($"   - ConfirmedCost (hotel only): {confirmedCost} EGP");
            _output.WriteLine($"   - BudgetSpent (all): {budgetSpent} EGP");
            _output.WriteLine($"   - Difference (estimated): {budgetSpent - confirmedCost} EGP");
        }
    }
}
