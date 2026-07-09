namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    /// <summary>
    /// تحذيرات البادجت
    /// </summary>
    public class BudgetWarning
    {
        /// <summary>
        /// نوع التحذير: Info, Warning, Critical
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// الرسالة الرئيسية
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// تفاصيل إضافية
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// المبلغ الزائد (لو موجود)
        /// </summary>
        public decimal? ExcessAmount { get; set; }

        /// <summary>
        /// نسبة البادجت المستخدمة
        /// </summary>
        public decimal? PercentageUsed { get; set; }

        public static BudgetWarning Info(string message, string? details = null)
        {
            return new BudgetWarning
            {
                Severity = "Info",
                Message = message,
                Details = details
            };
        }

        public static BudgetWarning Warning(string message, string? details = null, decimal? excess = null)
        {
            return new BudgetWarning
            {
                Severity = "Warning",
                Message = message,
                Details = details,
                ExcessAmount = excess
            };
        }

        public static BudgetWarning Critical(string message, string? details = null, decimal? excess = null)
        {
            return new BudgetWarning
            {
                Severity = "Critical",
                Message = message,
                Details = details,
                ExcessAmount = excess
            };
        }
    }
}
