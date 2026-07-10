namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    /// <summary>
    /// Detailed budget breakdown by category
    /// </summary>
    public class BudgetBreakdownDto
    {
        public BudgetCategoryDto Hotel { get; set; } = new();
        public BudgetCategoryDto Flight { get; set; } = new();
        public BudgetCategoryDto Activities { get; set; } = new();
    }

    /// <summary>
    /// Budget information for a specific category
    /// </summary>
    public class BudgetCategoryDto
    {
        /// <summary>
        /// Amount allocated from budget for this category
        /// </summary>
        public decimal Allocated { get; set; }

        /// <summary>
        /// Actual amount (confirmed or estimated)
        /// </summary>
        public decimal Actual { get; set; }

        /// <summary>
        /// Status: "confirmed" (actual price) or "estimated" (calculated estimate)
        /// </summary>
        public string Status { get; set; } = "estimated";

        /// <summary>
        /// Difference between allocated and actual (negative = under budget)
        /// </summary>
        public decimal Difference => Actual - Allocated;

        /// <summary>
        /// Percentage of allocated budget used
        /// </summary>
        public decimal PercentageUsed => Allocated > 0 ? (Actual / Allocated) * 100 : 0;
    }
}
