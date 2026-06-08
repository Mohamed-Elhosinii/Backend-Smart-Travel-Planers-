using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    public class ForgotPasswordDto
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }
}
