using System;

namespace SmartTravelPlaners.BLL.Features.Admin.DTOs
{
    public class AdminUserListItemDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public Guid PlanId { get; set; }
        public string SubscriptionStatus { get; set; } = string.Empty;
        public bool IsBanned { get; set; }
    }
}
