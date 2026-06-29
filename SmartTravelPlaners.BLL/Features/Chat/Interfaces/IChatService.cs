using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.BLL.Features.Chat.Interfaces
{
    public interface IChatService
    {
        Task<ChatReplyDto> SendMessageAsync(Guid sessionId, string userId, string userMessage);

        Task<ChatSession> CreateSessionAsync(string userId);

        Task<List<ChatSession>> GetUserSessionsAsync(string userId);

        Task<List<ChatMessage>> GetHistoryAsync(Guid sessionId, string userId);

        Task<TripPlanDto?> GetTripPlanAsync(Guid tripId, string userId);

        Task LinkSessionToTripAsync(Guid sessionId, string userId, Guid tripId);
    }
}