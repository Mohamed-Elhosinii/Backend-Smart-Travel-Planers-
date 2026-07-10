using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlaners.BLL.Features.Chat.DTOs
{
    /// <summary>
    /// Trip-creation payload. Parsed from the AI's <c>TRIP_READY</c> marker (chat path)
    /// and bound from the request body of <c>POST /api/Trip/quick-plan</c> (form path).
    ///
    /// DataAnnotations + <see cref="IValidatableObject"/> are honoured by [ApiController]
    /// model binding (→ 400 ProblemDetails) on the form path. They are NOT run by
    /// System.Text.Json, so the chat path is unaffected by them.
    /// </summary>
    public class TripCreateDto : IValidatableObject
    {
        [Required]
        public string Destination { get; set; } = string.Empty;

        public string? DestId { get; set; }
        public string? DestType { get; set; }

        /// <summary>Optional. Null/empty → the trip is planned without a flight.</summary>
        public string? OriginCity { get; set; }

        [Required]
        public string StartDate { get; set; } = string.Empty;

        [Required]
        public string EndDate { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "NumTravelers must be at least 1.")]
        public int NumTravelers { get; set; }

        [Range(100, double.MaxValue, ErrorMessage = "BudgetTotal must be at least 100 USD.")]
        public decimal BudgetTotal { get; set; }

        public bool? IsRoundTrip { get; set; }

        public List<string> Preferences { get; set; } = new();

        /// <summary>Cross-field rules: valid dates, EndDate ≥ StartDate, BudgetTotal &gt; 0.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var startOk = DateOnly.TryParse(StartDate, out var start);
            var endOk = DateOnly.TryParse(EndDate, out var end);

            if (!startOk)
                yield return new ValidationResult(
                    "StartDate must be a valid date (yyyy-MM-dd).", new[] { nameof(StartDate) });

            if (!endOk)
                yield return new ValidationResult(
                    "EndDate must be a valid date (yyyy-MM-dd).", new[] { nameof(EndDate) });

            if (startOk && endOk && end < start)
                yield return new ValidationResult(
                    "EndDate must be on or after StartDate.", new[] { nameof(EndDate) });

            if (BudgetTotal <= 0)
                yield return new ValidationResult(
                    "BudgetTotal must be greater than 0.", new[] { nameof(BudgetTotal) });

            // Minimum budget: 100 USD
            const decimal MinimumBudget = 100m;
            if (BudgetTotal < MinimumBudget)
                yield return new ValidationResult(
                    $"BudgetTotal must be at least {MinimumBudget:F0} USD to create a meaningful trip plan.",
                    new[] { nameof(BudgetTotal) });
        }
    }
}
