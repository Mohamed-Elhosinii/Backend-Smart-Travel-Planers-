using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Chat.Interfaces;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        // POST api/chat/session
        [HttpPost("session")]
        public async Task<IActionResult> CreateSession()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized chat session creation attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Chat session creation initiated for UserId: {UserId}", userId);
                var session = await _chatService.CreateSessionAsync(userId);
                _logger.LogInformation("Chat session created successfully for UserId: {UserId}, SessionId: {SessionId}", userId, session.Id);
                return Ok(new { sessionId = session.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat session creation failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        // POST api/chat/send
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
            {
                _logger.LogWarning("Message send attempt with empty message");
                return BadRequest("Message is empty");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized message send attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Message processing started for UserId: {UserId}, SessionId: {SessionId}", userId, dto.SessionId);
                var reply = await _chatService.SendMessageAsync(dto.SessionId, userId, dto.Message);
                _logger.LogInformation("Message processed successfully for UserId: {UserId}, SessionId: {SessionId}", userId, dto.SessionId);
                return Ok(reply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message processing failed for UserId: {UserId}, SessionId: {SessionId}. Error: {ErrorMessage}", userId, dto.SessionId, ex.Message);
                throw;
            }
        }

        // GET api/chat/sessions
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized chat sessions retrieval attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Chat sessions retrieval requested for UserId: {UserId}", userId);
                var sessions = await _chatService.GetUserSessionsAsync(userId);

                // Map to a simpler DTO if needed, or return entities
                var result = sessions.Select(s => new
                {
                    id = s.Id,
                    date = s.UpdatedAt.ToString("MMM dd, yyyy"),
                    title = string.IsNullOrWhiteSpace(s.Title) ? "New Journey" : s.Title,
                    tripId = s.TripId
                });

                _logger.LogInformation("Chat sessions retrieved successfully for UserId: {UserId}. Count: {SessionsCount}", userId, sessions.Count());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat sessions retrieval failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        // GET api/chat/history/{sessionId}
        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetHistory(Guid sessionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized chat history retrieval attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Chat history retrieval requested for UserId: {UserId}, SessionId: {SessionId}", userId, sessionId);
                var messages = await _chatService.GetHistoryAsync(sessionId, userId);
                _logger.LogInformation("Chat history retrieved successfully for UserId: {UserId}, SessionId: {SessionId}. MessagesCount: {MessagesCount}", userId, sessionId, messages.Count());
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat history retrieval failed for UserId: {UserId}, SessionId: {SessionId}. Error: {ErrorMessage}", userId, sessionId, ex.Message);
                throw;
            }
        }

        // GET api/chat/plan/{tripId}
        [HttpGet("plan/{tripId}")]
        public async Task<IActionResult> GetPlan(Guid tripId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized trip plan retrieval attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Trip plan retrieval requested for UserId: {UserId}, TripId: {TripId}", userId, tripId);
                var plan = await _chatService.GetTripPlanAsync(tripId, userId);
                if (plan == null)
                {
                    _logger.LogWarning("Trip plan not ready or access denied for UserId: {UserId}, TripId: {TripId}", userId, tripId);
                    return NotFound("Plan not ready yet or access denied.");
                }

                _logger.LogInformation("Trip plan retrieved successfully for UserId: {UserId}, TripId: {TripId}", userId, tripId);
                return Ok(plan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trip plan retrieval failed for UserId: {UserId}, TripId: {TripId}. Error: {ErrorMessage}", userId, tripId, ex.Message);
                throw;
            }
        }

        //// POST api/chat/session/link-trip
        //[HttpPost("session/link-trip")]
        //public async Task<IActionResult> LinkSessionToTrip([FromBody] LinkTripDto dto)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    if (userId == null) return Unauthorized();

        //    await _chatService.LinkSessionToTripAsync(dto.SessionId, dto.TripId, userId);
        //    return Ok();
        //}
        [HttpPost("session/trip")]
        public async Task<IActionResult> GetOrCreateTripSession([FromBody] TripSessionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized trip session creation attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Trip session creation/retrieval initiated for UserId: {UserId}, TripId: {TripId}", userId, dto.TripId);
                var session = await _chatService.GetOrCreateTripSessionAsync(dto.TripId, userId);
                _logger.LogInformation("Trip session created/retrieved successfully for UserId: {UserId}, TripId: {TripId}, SessionId: {SessionId}", userId, dto.TripId, session.Id);
                return Ok(new
                {
                    sessionId = session.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trip session creation/retrieval failed for UserId: {UserId}, TripId: {TripId}. Error: {ErrorMessage}", userId, dto.TripId, ex.Message);
                throw;
            }
        }
    }
    public class TripSessionDto
    {
        public Guid TripId { get; set; }
    }

    public class LinkTripDto
    {
        public Guid SessionId { get; set; }
        public Guid TripId { get; set; }
    }
    public class SendMessageDto
    {
        public Guid SessionId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

}