using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.BLL.Features.Chat.Interfaces
{
    public interface IChatService
    {
        Task<ChatReplyDto> SendMessageAsync(Guid sessionId, string userId, string userMessage);

        Task<ChatSession> CreateSessionAsync(string userId);

        Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId, string userId);

        Task<List<ChatSession>> GetUserSessionsAsync(string userId);
    }
}