using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.Services
{
    /// <summary>
    /// Budget validation logic - validates and generates warnings
    /// </summary>
    public static class BudgetValidator
    {
        /// <summary>
        /// Validate trip budget and generate warnings
        /// </summary>
        public static List<BudgetWarning> ValidateBudget(
            decimal budgetTotal,
            decimal budgetSpent,
            decimal confirmedCost,
            decimal estimatedTotalCost)
        {
            var warnings = new List<BudgetWarning>();

            if (budgetTotal <= 0)
            {
                warnings.Add(BudgetWarning.Warning(
                    "No budget set for this trip",
                    "Consider setting a budget to track your expenses better."
                ));
                return warnings;
            }

            var percentageUsed = (budgetSpent / budgetTotal) * 100;
            var remaining = budgetTotal - budgetSpent;

            // Critical: Budget exceeded
            if (budgetSpent > budgetTotal)
            {
                var excess = budgetSpent - budgetTotal;
                warnings.Add(BudgetWarning.Critical(
                    $"Budget exceeded by {excess:F2} EGP",
                    $"Your total spending ({budgetSpent:F2} EGP) has exceeded your budget ({budgetTotal:F2} EGP).",
                    excess
                ));
            }
            // Warning: Over 90% used
            else if (percentageUsed >= 90)
            {
                warnings.Add(BudgetWarning.Warning(
                    $"You've used {percentageUsed:F1}% of your budget",
                    $"Only {remaining:F2} EGP remaining. Consider adjusting your plans.",
                    null
                ));
            }
            // Info: Over 75% used
            else if (percentageUsed >= 75)
            {
                warnings.Add(BudgetWarning.Info(
                    $"You've used {percentageUsed:F1}% of your budget",
                    $"You have {remaining:F2} EGP remaining."
                ));
            }

            // Warning: Confirmed cost alone exceeds budget
            if (confirmedCost > budgetTotal)
            {
                var excess = confirmedCost - budgetTotal;
                warnings.Add(BudgetWarning.Critical(
                    $"Confirmed bookings exceed budget by {excess:F2} EGP",
                    "Your hotel booking alone has exceeded your total budget. You may need to increase your budget or choose a cheaper option.",
                    excess
                ));
            }

            // Info: High confirmed vs estimated ratio
            if (budgetSpent > 0 && confirmedCost > 0)
            {
                var confirmedPercentage = (confirmedCost / budgetSpent) * 100;
                if (confirmedPercentage < 30)
                {
                    warnings.Add(BudgetWarning.Info(
                        "Most of your budget is estimated",
                        $"Only {confirmedPercentage:F1}% of your spending is confirmed. Actual costs may vary."
                    ));
                }
            }

            // Warning: Estimated total exceeds budget
            if (estimatedTotalCost > budgetTotal && budgetSpent <= budgetTotal)
            {
                var excess = estimatedTotalCost - budgetTotal;
                warnings.Add(BudgetWarning.Warning(
                    $"Estimated costs exceed budget by {excess:F2} EGP",
                    "Based on current estimates, you may go over budget. Consider adjusting your plans.",
                    excess
                ));
            }

            // Info: Good budget management
            if (warnings.Count == 0 && percentageUsed > 0 && percentageUsed < 75)
            {
                warnings.Add(BudgetWarning.Info(
                    "Budget on track",
                    $"You're using {percentageUsed:F1}% of your budget. {remaining:F2} EGP remaining."
                ));
            }

            return warnings;
        }

        /// <summary>
        /// Validate day budget
        /// </summary>
        public static List<BudgetWarning> ValidateDayBudget(
            int dayNumber,
            decimal budgetAllocated,
            decimal budgetSpent)
        {
            var warnings = new List<BudgetWarning>();

            if (budgetAllocated <= 0)
            {
                return warnings;
            }

            if (budgetSpent > budgetAllocated)
            {
                var excess = budgetSpent - budgetAllocated;
                warnings.Add(BudgetWarning.Warning(
                    $"Day {dayNumber}: Budget exceeded by {excess:F2} EGP",
                    $"This day's activities ({budgetSpent:F2} EGP) exceed the allocated budget ({budgetAllocated:F2} EGP).",
                    excess
                ));
            }
            else if (budgetSpent > budgetAllocated * 0.9m)
            {
                warnings.Add(BudgetWarning.Info(
                    $"Day {dayNumber}: Almost at budget limit",
                    $"You've used {((budgetSpent / budgetAllocated) * 100):F1}% of this day's budget."
                ));
            }

            return warnings;
        }

        /// <summary>
        /// Check if hotel is too expensive for budget
        /// </summary>
        public static BudgetWarning? ValidateHotelCost(
            decimal hotelTotalCost,
            decimal budgetTotal,
            bool hasOriginCity)
        {
            if (budgetTotal <= 0 || hotelTotalCost <= 0)
            {
                return null;
            }

            var expectedHotelShare = hasOriginCity ? 0.40m : 0.52m; // 40% with flight, 52% without
            var expectedHotelBudget = budgetTotal * expectedHotelShare;

            if (hotelTotalCost > expectedHotelBudget * 1.2m) // 20% over expected
            {
                var excess = hotelTotalCost - expectedHotelBudget;
                return BudgetWarning.Warning(
                    "Hotel may be too expensive for your budget",
                    $"Your hotel costs {hotelTotalCost:F2} EGP, which is {excess:F2} EGP more than the recommended {expectedHotelBudget:F2} EGP for your budget.",
                    excess
                );
            }

            return null;
        }
    }
}
