using Microsoft.AspNetCore.Identity;

namespace SmartTravelPlaners.DAL.Entities
{
  
    public class ApplicationUser : IdentityUser
    {
        public UserProfile? Profile { get; set; }
        public ICollection<ChatSession> ChatSessions { get; set; } = [];
    }
}
