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
        private readonly IChatService _chatService;   // <-- IChatService بدل ChatService

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

            var reply = await _chatService.SendMessageAsync(dto.SessionId, dto.Message);
            return Ok(new { reply });
        }

        // GET api/chat/history/{sessionId}
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