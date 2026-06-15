using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.BLL.Features.Chat.Interfaces
{
    public interface IChatService
    {
        Task<ChatReplyDto> SendMessageAsync(Guid sessionId, string userMessage);

        Task<ChatSession> CreateSessionAsync(string userId);

        Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId);
    }
}