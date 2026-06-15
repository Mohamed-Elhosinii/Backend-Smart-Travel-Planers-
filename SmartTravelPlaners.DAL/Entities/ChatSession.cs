using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.DAL.Entities
{
    public class ChatSession
    {
        public Guid Id { get; set; }

        // nullable - gets assigned after AI creates the Trip
        public Guid? TripId { get; set; }
        public Trip? Trip { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        // tracks where the conversation is
        public ChatStage Stage { get; set; } = ChatStage.CollectingInfo;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ChatMessage> Messages { get; set; } = new HashSet<ChatMessage>();
    }
}