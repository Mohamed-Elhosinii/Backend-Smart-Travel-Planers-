using Microsoft.EntityFrameworkCore;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class ChatRepository : IChatRepository
    {
        private readonly ApplicationDbContext _context;

        public ChatRepository(ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task<ChatSession?> GetSessionAsync(Guid sessionId)
        {
            return await _context.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }
        public async Task<ChatSession?> GetSessionByUserAsync(string userId)
        {
            return await _context.ChatSessions
                .Include(s => s.Messages)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ChatSession>> GetSessionsByUserAsync(string userId)
        {
            return await _context.ChatSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync();
        }
        // create a new session without a trip - trip gets linked later
        public async Task<ChatSession> CreateSessionAsync(string userId)
        {
            var session = new ChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TripId = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.ChatSessions.AddAsync(session);
            return session;
        }

        public async Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId)
        {
            // AsNoTracking → returns standalone messages with the Session nav left null,
            // avoiding the ChatMessage.Session ↔ ChatSession.Messages serialization cycle
            // (the ownership check in GetHistoryAsync otherwise tracks & fixes up Session).
            return await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task AddMessageAsync(ChatMessage message)
        {
            await _context.ChatMessages.AddAsync(message);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
        public async Task<ChatSession?> GetSessionByTripIdAsync(Guid tripId, string userId)
        {
            return await _context.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s =>
                    s.TripId == tripId &&
                    s.UserId == userId);
        }
    }
}