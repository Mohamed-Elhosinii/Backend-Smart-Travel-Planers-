using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime AccessTokenExpiry { get; set; }
    }
}
