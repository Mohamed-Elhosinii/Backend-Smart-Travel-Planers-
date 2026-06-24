using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Interfaces;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.PL.Controllers;
using System.Security.Claims;
using Xunit;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class ChatControllerTests
    {
        private readonly Mock<IChatService> _serviceMock;
        private readonly ChatController _controller;

        public ChatControllerTests()
        {
            _serviceMock = new Mock<IChatService>();
            _controller = new ChatController(_serviceMock.Object);
        }

        private void SetupUser(string userId = "user-1")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private void SetupNoUser()
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };
        }

        // ============================================================
        // CreateSession
        // ============================================================

        [Fact]
        public async Task CreateSession_ShouldReturn401_WhenNoUser()
        {
            SetupNoUser();

            var result = await _controller.CreateSession() as UnauthorizedResult;

            Assert.NotNull(result);
            Assert.Equal(401, result!.StatusCode);
        }

        [Fact]
        public async Task CreateSession_ShouldReturn200_WhenUserExists()
        {
            SetupUser("user-1");
            _serviceMock.Setup(s => s.CreateSessionAsync("user-1"))
                .ReturnsAsync(new ChatSession
                {
                    Id = Guid.NewGuid(),
                    UserId = "user-1",
                    Messages = new List<ChatMessage>()
                });

            var result = await _controller.CreateSession() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // Send
        // ============================================================

        [Fact]
        public async Task Send_ShouldReturn400_WhenMessageEmpty()
        {
            var dto = new SendMessageDto { SessionId = Guid.NewGuid(), Message = "" };

            var result = await _controller.Send(dto) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        [Fact]
        public async Task Send_ShouldReturn200_WhenSuccess()
        {
            var sessionId = Guid.NewGuid();
            _serviceMock.Setup(s => s.SendMessageAsync(sessionId,  "مرحبا"))
                .ReturnsAsync(new ChatReplyDto { Message = "أهلاً!" });

            var result = await _controller.Send(new SendMessageDto
            {
                SessionId = sessionId,
                Message = "مرحبا"
            }) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // GetHistory
        // ============================================================

        [Fact]
        public async Task GetHistory_ShouldReturn200_WithMessages()
        {
            var sessionId = Guid.NewGuid();
            _serviceMock.Setup(s => s.GetHistoryAsync(sessionId))
                .ReturnsAsync(new List<ChatMessage>
                {
                    new() { Id = Guid.NewGuid(), SessionId = sessionId,
                            Role = MessageRole.User, Content = "مرحبا" }
                });

            var result = await _controller.GetHistory(sessionId) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task GetHistory_ShouldReturn200_WhenEmpty()
        {
            var sessionId = Guid.NewGuid();
            _serviceMock.Setup(s => s.GetHistoryAsync(sessionId))
                .ReturnsAsync(new List<ChatMessage>());

            var result = await _controller.GetHistory(sessionId) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }
    }
}