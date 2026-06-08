using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    
    public class UserProfileDto
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
    }
}
