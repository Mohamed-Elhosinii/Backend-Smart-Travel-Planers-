namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    /// <summary>
    /// Represents a budget warning or notification
    /// </summary>
    public class BudgetWarning
    {
        /// <summary>
        /// Severity level: Info, Warning, Critical
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Short warning message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Detailed explanation
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Amount exceeding budget (if applicable)
        /// </summary>
        public decimal? ExcessAmount { get; set; }

        // Factory methods for creating warnings
        public static BudgetWarning Info(string message, string details)
        {
            return new BudgetWarning
            {
                Severity = "Info",
                Message = message,
                Details = details,
                ExcessAmount = null
            };
        }

        public static BudgetWarning Warning(string message, string details, decimal? excessAmount = null)
        {
            return new BudgetWarning
            {
                Severity = "Warning",
                Message = message,
                Details = details,
                ExcessAmount = excessAmount
            };
        }

        public static BudgetWarning Critical(string message, string details, decimal? excessAmount = null)
        {
            return new BudgetWarning
            {
                Severity = "Critical",
                Message = message,
                Details = details,
                ExcessAmount = excessAmount
            };
        }
    }
}
