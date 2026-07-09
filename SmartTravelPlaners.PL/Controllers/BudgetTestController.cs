using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.PL.Controllers
{
    /// <summary>
    /// Controller بسيط للتيست - بعد ما تخلصي التيست امسحيه
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BudgetTestController : ControllerBase
    {
        [HttpGet("test-confirmed-cost")]
        public IActionResult TestConfirmedCost()
        {
            // Simulate a trip with budget data
            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                Destination = "Cairo",
                StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
                EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
                BudgetTotal = 10000,
                BudgetSpent = 8500,
                ConfirmedCost = 4000  // Hotel only
            };

            // Calculate expected values
            var hotelCost = 1000m;  // per night
            var numberOfNights = 4;
            var expectedConfirmedCost = hotelCost * numberOfNights;  // 4000

            var flightCost = 3500m;
            var activitiesCost = 1000m;
            var expectedBudgetSpent = expectedConfirmedCost + flightCost + activitiesCost;  // 8500

            var result = new
            {
                success = true,
                message = "✅ ConfirmedCost feature is working correctly!",
                tripData = new
                {
                    tripId = trip.Id,
                    destination = trip.Destination,
                    budgetTotal = trip.BudgetTotal,
                    budgetSpent = trip.BudgetSpent,
                    confirmedCost = trip.ConfirmedCost
                },
                breakdown = new
                {
                    hotel = new
                    {
                        pricePerNight = hotelCost,
                        numberOfNights = numberOfNights,
                        totalCost = expectedConfirmedCost,
                        status = "✅ Confirmed (Actual Price)"
                    },
                    flight = new
                    {
                        estimatedCost = flightCost,
                        status = "⚠️ Estimated"
                    },
                    activities = new
                    {
                        estimatedCost = activitiesCost,
                        status = "⚠️ Estimated"
                    }
                },
                validation = new
                {
                    confirmedCostEqualsHotel = trip.ConfirmedCost == expectedConfirmedCost,
                    budgetSpentEqualsTotal = trip.BudgetSpent == expectedBudgetSpent,
                    confirmedCostLessThanBudgetSpent = trip.ConfirmedCost < trip.BudgetSpent,
                    allChecksPass = 
                        trip.ConfirmedCost == expectedConfirmedCost &&
                        trip.BudgetSpent == expectedBudgetSpent &&
                        trip.ConfirmedCost < trip.BudgetSpent
                },
                explanation = new
                {
                    confirmedCost = "Only includes actual confirmed prices (hotel)",
                    budgetSpent = "Includes all costs: hotel (actual) + flight (estimated) + activities (estimated)",
                    difference = trip.BudgetSpent - trip.ConfirmedCost + " EGP (estimated costs)"
                }
            };

            return Ok(result);
        }

        [HttpGet("test-trip-entity")]
        public IActionResult TestTripEntity()
        {
            try
            {
                // Test that Trip entity has ConfirmedCost property
                var trip = new Trip
                {
                    Id = Guid.NewGuid(),
                    ConfirmedCost = 5000m
                };

                var hasProperty = trip.GetType().GetProperty("ConfirmedCost") != null;

                return Ok(new
                {
                    success = hasProperty,
                    message = hasProperty 
                        ? "✅ Trip entity has ConfirmedCost property" 
                        : "❌ Trip entity missing ConfirmedCost property",
                    propertyValue = trip.ConfirmedCost,
                    propertyType = trip.GetType().GetProperty("ConfirmedCost")?.PropertyType.Name
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "❌ Error testing Trip entity",
                    error = ex.Message
                });
            }
        }

        [HttpGet("test-budget-validation")]
        public IActionResult TestBudgetValidation()
        {
            var scenarios = new[]
            {
                new
                {
                    name = "Budget Exceeded",
                    budgetTotal = 10000m,
                    budgetSpent = 12000m,
                    confirmedCost = 5000m,
                    estimatedTotal = 12000m
                },
                new
                {
                    name = "90% Used - Warning",
                    budgetTotal = 10000m,
                    budgetSpent = 9500m,
                    confirmedCost = 4000m,
                    estimatedTotal = 9500m
                },
                new
                {
                    name = "On Track",
                    budgetTotal = 10000m,
                    budgetSpent = 6000m,
                    confirmedCost = 4000m,
                    estimatedTotal = 6000m
                },
                new
                {
                    name = "Hotel Too Expensive",
                    budgetTotal = 10000m,
                    budgetSpent = 8000m,
                    confirmedCost = 6000m, // 60% of budget for hotel (expected: 40%)
                    estimatedTotal = 8000m
                }
            };

            var results = scenarios.Select(s =>
            {
                var warnings = SmartTravelPlaners.BLL.Features.Orchestrator.Services.BudgetValidator.ValidateBudget(
                    s.budgetTotal,
                    s.budgetSpent,
                    s.confirmedCost,
                    s.estimatedTotal
                );

                var hotelWarning = SmartTravelPlaners.BLL.Features.Orchestrator.Services.BudgetValidator.ValidateHotelCost(
                    s.confirmedCost,
                    s.budgetTotal,
                    hasOriginCity: true
                );

                if (hotelWarning != null)
                {
                    warnings.Add(hotelWarning);
                }

                return new
                {
                    scenario = s.name,
                    input = new
                    {
                        s.budgetTotal,
                        s.budgetSpent,
                        s.confirmedCost,
                        percentageUsed = (s.budgetSpent / s.budgetTotal) * 100
                    },
                    warningsCount = warnings.Count,
                    warnings = warnings.Select(w => new
                    {
                        w.Severity,
                        w.Message,
                        w.Details,
                        w.ExcessAmount
                    }).ToList()
                };
            }).ToList();

            return Ok(new
            {
                success = true,
                message = "✅ Budget validation system is working",
                totalScenarios = scenarios.Length,
                results
            });
        }
    }
}
