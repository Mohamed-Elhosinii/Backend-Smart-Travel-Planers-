using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Repositories.Abstract
{
    public interface IChatRepository
    {
        // Sessions
        Task<ChatSession?> GetSessionAsync(Guid sessionId);
        Task<ChatSession> CreateSessionAsync(string userId);
        Task<ChatSession?> GetSessionByUserAsync(string userId);
        Task<List<ChatSession>> GetSessionsByUserAsync(string userId);

        // Messages
        Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId);
        Task AddMessageAsync(ChatMessage message);

        Task SaveChangesAsync();
        Task<ChatSession?> GetSessionByTripIdAsync(Guid tripId, string userId);
    }
}