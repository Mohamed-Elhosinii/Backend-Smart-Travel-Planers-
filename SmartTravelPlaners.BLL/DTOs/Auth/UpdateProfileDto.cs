using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    public class UpdateProfileDto
    {
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }
    }
}
