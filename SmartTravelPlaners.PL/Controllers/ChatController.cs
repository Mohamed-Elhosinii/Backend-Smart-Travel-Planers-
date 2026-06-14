using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Services.Concrete;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;

        public ChatController(ChatService chatService)
        {
            _chatService = chatService;
        }

        // POST api/chat/session
        // creates or returns existing session for the logged-in user
        [HttpPost("session")]
        public async Task<IActionResult> CreateSession()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var session = await _chatService.CreateSessionAsync(userId);
            return Ok(new { sessionId = session.Id });
        }

        // POST api/chat/send
        // sends a message and returns AI reply
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest("Message is empty");

            var reply = await _chatService.SendMessageAsync(dto.SessionId, dto.Message);
            return Ok(new { reply });
        }

        // GET api/chat/history/{sessionId}
        // returns all messages for a session
        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetHistory(Guid sessionId)
        {
            var messages = await _chatService.GetHistoryAsync(sessionId);
            return Ok(messages);
        }
    }

    public class SendMessageDto
    {
        public Guid SessionId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}