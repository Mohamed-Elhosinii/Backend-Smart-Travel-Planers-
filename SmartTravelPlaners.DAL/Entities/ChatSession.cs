namespace SmartTravelPlaners.DAL.Entities
{
  
    public class ChatSession
    {
        public Guid Id { get; set; }

        // FK → Trip (one-to-one)
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        // FK → ApplicationUser
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<ChatMessage> Messages { get; set; } = new HashSet<ChatMessage>();
    }
}
