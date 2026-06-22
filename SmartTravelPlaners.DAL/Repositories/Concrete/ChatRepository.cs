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

        // manually map session to avoid EF nullable Guid bug
        public async Task<ChatSession?> GetSessionAsync(Guid sessionId)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            ChatSession? session = null;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Id, UserId, TripId, Stage, CreatedAt, UpdatedAt
                    FROM ChatSessions
                    WHERE Id = @id";

                var param = cmd.CreateParameter();
                param.ParameterName = "@id";
                param.Value = sessionId;
                cmd.Parameters.Add(param);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    session = new ChatSession
                    {
                        Id = reader.GetGuid(0),
                        UserId = reader.GetString(1),
                        TripId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                        Stage = (ChatStage)reader.GetInt32(3),
                        CreatedAt = reader.GetDateTime(4),
                        UpdatedAt = reader.GetDateTime(5)
                    };
                }
            }

            if (session == null) return null;

            // load messages separately
            session.Messages = await _context.ChatMessages
                .Where(m => m.SessionId == session.Id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return session;
        }

        // manually map to avoid EF nullable Guid bug
        public async Task<ChatSession?> GetSessionByUserAsync(string userId)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            ChatSession? session = null;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TOP 1 Id, UserId, TripId, Stage, CreatedAt, UpdatedAt
                    FROM ChatSessions
                    WHERE UserId = @userId";

                var param = cmd.CreateParameter();
                param.ParameterName = "@userId";
                param.Value = userId;
                cmd.Parameters.Add(param);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    session = new ChatSession
                    {
                        Id = reader.GetGuid(0),
                        UserId = reader.GetString(1),
                        TripId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                        Stage = (ChatStage)reader.GetInt32(3),
                        CreatedAt = reader.GetDateTime(4),
                        UpdatedAt = reader.GetDateTime(5)
                    };
                }
            }

            if (session == null) return null;

            // load messages separately
            session.Messages = await _context.ChatMessages
                .Where(m => m.SessionId == session.Id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return session;
        }

        public async Task<List<ChatSession>> GetSessionsByUserAsync(string userId)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            var sessions = new List<ChatSession>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Id, UserId, TripId, Stage, CreatedAt, UpdatedAt
                    FROM ChatSessions
                    WHERE UserId = @userId
                    ORDER BY UpdatedAt DESC";

                var param = cmd.CreateParameter();
                param.ParameterName = "@userId";
                param.Value = userId;
                cmd.Parameters.Add(param);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sessions.Add(new ChatSession
                    {
                        Id = reader.GetGuid(0),
                        UserId = reader.GetString(1),
                        TripId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                        Stage = (ChatStage)reader.GetInt32(3),
                        CreatedAt = reader.GetDateTime(4),
                        UpdatedAt = reader.GetDateTime(5)
                    });
                }
            }

            return sessions;
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
            return await _context.ChatMessages
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
    }
}