using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    
    public class UserProfileDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? CurrentPlan { get; set; }
    }
}
