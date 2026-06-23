using System;

namespace SmartTravelPlaners.BLL.Features.Admin.DTOs
{
    public class AdminPaymentTransactionDto
    {
        public Guid Id { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string PaymobOrderId { get; set; } = string.Empty;
        public string? PaymobTransactionId { get; set; }
    }
}
