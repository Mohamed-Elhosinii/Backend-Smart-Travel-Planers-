using Microsoft.AspNetCore.Identity;

namespace SmartTravelPlaners.DAL.Entities
{
  
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = null!;
        public UserProfile? Profile { get; set; }
        public ICollection<ChatSession> ChatSessions { get; set; } = [];

        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    }
}
