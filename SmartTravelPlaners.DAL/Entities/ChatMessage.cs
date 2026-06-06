using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.DAL.Entities
{
    public class ChatMessage
    {
        public Guid Id { get; set; }

        // FK → ChatSession
        public Guid SessionId { get; set; }
        public ChatSession Session { get; set; } = null!;

        public MessageRole Role { get; set; }
        public string Content { get; set; } = string.Empty;

    
        public string? ToolCallsJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
