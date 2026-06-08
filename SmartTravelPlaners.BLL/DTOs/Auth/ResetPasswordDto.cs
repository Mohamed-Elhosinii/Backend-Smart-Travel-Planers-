using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    public class ResetPasswordDto
    {
        [Required] public string UserId { get; set; }
        [Required] public string Token { get; set; }
        [Required, MinLength(6)] public string NewPassword { get; set; }
        [Compare("NewPassword")] public string ConfirmPassword { get; set; }
    }
}
