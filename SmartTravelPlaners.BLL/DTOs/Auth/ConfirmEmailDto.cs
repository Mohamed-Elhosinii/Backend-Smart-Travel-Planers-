using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    public class ConfirmEmailDto
    {
        [Required] public string UserId { get; set; }
        [Required] public string Token { get; set; }
    }
}
