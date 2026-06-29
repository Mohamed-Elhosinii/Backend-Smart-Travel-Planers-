using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        // POST api/chat/session
        [HttpPost("session")]
        public async Task<IActionResult> CreateSession()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var session = await _chatService.CreateSessionAsync(userId);
            return Ok(new { sessionId = session.Id });
        }

        // POST api/chat/send
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest("Message is empty");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var reply = await _chatService.SendMessageAsync(dto.SessionId, userId, dto.Message);
            return Ok(reply);
        }

        // GET api/chat/sessions
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var sessions = await _chatService.GetUserSessionsAsync(userId);

            var result = sessions.Select(s => new {
                id = s.Id,
                date = s.UpdatedAt.ToString("MMM dd, yyyy"),
                title = s.TripId != null ? "Trip Details" : "New Journey",
                tripId = s.TripId
            });

            return Ok(result);
        }

        // GET api/chat/history/{sessionId}
        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetHistory(Guid sessionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var messages = await _chatService.GetHistoryAsync(sessionId, userId);
            return Ok(messages);
        }

        // GET api/chat/plan/{tripId}
        [HttpGet("plan/{tripId}")]
        public async Task<IActionResult> GetPlan(Guid tripId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var plan = await _chatService.GetTripPlanAsync(tripId, userId);
            if (plan == null) return NotFound("Plan not ready yet or access denied.");
            return Ok(plan);
        }

        // POST api/chat/session/link-trip
        [HttpPost("session/link-trip")]
        public async Task<IActionResult> LinkTrip([FromBody] LinkTripDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                await _chatService.LinkSessionToTripAsync(dto.SessionId, userId, dto.TripId);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "no inner exception";
                return BadRequest(new { error = ex.Message, inner });
            }
        }
    }

    public class SendMessageDto
    {
        public Guid SessionId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class LinkTripDto
    {
        public Guid SessionId { get; set; }
        public Guid TripId { get; set; }
    }
}