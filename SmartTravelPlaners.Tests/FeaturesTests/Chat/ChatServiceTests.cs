using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Services;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Chat
{
    public class ChatServiceTests
    {
        private readonly Mock<IChatRepository> _chatRepoMock;
        private readonly Mock<ITripRepository> _tripRepoMock;
        private readonly Mock<IUserProfileRepository> _userProfileRepoMock;
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<ITripOrchestratorService> _orchestratorMock;
        private readonly Mock<IUsageLimitService> _usageLimitMock;
        private readonly Mock<IChatCompletionService> _aiMock;
        private readonly ChatService _service;

        public ChatServiceTests()
        {
            _chatRepoMock = new Mock<IChatRepository>();
            _tripRepoMock = new Mock<ITripRepository>();
            _userProfileRepoMock = new Mock<IUserProfileRepository>();
            _uowMock = new Mock<IUnitOfWork>();
            _orchestratorMock = new Mock<ITripOrchestratorService>();
            _usageLimitMock = new Mock<IUsageLimitService>();
            _aiMock = new Mock<IChatCompletionService>();

           
            var kernel = new Kernel();
            kernel.Services.GetServices<IChatCompletionService>();

            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(_aiMock.Object);
            var builtKernel = kernelBuilder.Build();

            _service = new ChatService(
                _chatRepoMock.Object,
                _tripRepoMock.Object,
                _userProfileRepoMock.Object,
                _uowMock.Object,
                _orchestratorMock.Object,
                _usageLimitMock.Object,
                builtKernel);
        }

        private ChatSession MakeSession(Guid? tripId = null) => new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            TripId = tripId,
            Stage = ChatStage.CollectingInfo,
            Messages = new List<ChatMessage>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private void SetupAiReply(string reply)
        {
            var content = new ChatMessageContent(AuthorRole.Assistant, reply);
            _aiMock.Setup(a => a.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ChatMessageContent> { content });
        }

        // ============================================================
        // SendMessageAsync — Session Not Found
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldThrow_WhenSessionNotFound()
        {
            var sessionId = Guid.NewGuid();
            _chatRepoMock.Setup(r => r.GetSessionAsync(sessionId)).ReturnsAsync((ChatSession?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.SendMessageAsync(sessionId, "مرحبا"));
        }

        // ============================================================
        // SendMessageAsync — Message Limit Reached
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldReturnLimitMessage_WhenMessageLimitReached()
        {
            var session = MakeSession();
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(false);

            var result = await _service.SendMessageAsync(session.Id, "مرحبا");

            Assert.Contains("limit", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ============================================================
        // SendMessageAsync — Normal Reply (no special format)
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldReturnAiReply_WhenNormalMessage()
        {
            var session = MakeSession();
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(true);
            _usageLimitMock.Setup(u => u.IncrementMessageUsageAsync(session.UserId)).Returns(Task.CompletedTask);
            _chatRepoMock.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
            _chatRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            SetupAiReply("أهلاً! ازيك؟");

            var result = await _service.SendMessageAsync(session.Id, "مرحبا");

            Assert.Equal("أهلاً! ازيك؟", result.Message);
        }

        // ============================================================
        // SendMessageAsync — TRIP_UPDATE_HOTEL with no trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldReturnNoTrip_WhenHotelUpdateWithNoTrip()
        {
            var session = MakeSession(tripId: null);
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(true);

            SetupAiReply("TRIP_UPDATE_HOTEL:{}");

            var result = await _service.SendMessageAsync(session.Id, "غيرلي الفندق");

            Assert.NotNull(result.Message);
            Assert.Contains("مفيش رحلة", result.Message);
        }

        // ============================================================
        // SendMessageAsync — TRIP_UPDATE_HOTEL with existing trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldRegenerateHotel_WhenTripExists()
        {
            var tripId = Guid.NewGuid();
            var session = MakeSession(tripId: tripId);
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(true);
            _usageLimitMock.Setup(u => u.IncrementMessageUsageAsync(session.UserId)).Returns(Task.CompletedTask);
            _chatRepoMock.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
            _chatRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            _orchestratorMock.Setup(o => o.RegenerateHotelAsync(tripId))
                .ReturnsAsync(new TripHotelDto { Name = "New Hotel" });
            _orchestratorMock.Setup(o => o.GetCurrentPlanAsync(tripId))
                .ReturnsAsync(new TripPlanDto());

            SetupAiReply("TRIP_UPDATE_HOTEL:{}");

            var result = await _service.SendMessageAsync(session.Id, "غيرلي الفندق");

            Assert.Contains("New Hotel", result.Message);
            _orchestratorMock.Verify(o => o.RegenerateHotelAsync(tripId), Times.Once);
        }

        // ============================================================
        // SendMessageAsync — TRIP_UPDATE_FLIGHT with no trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldReturnNoTrip_WhenFlightUpdateWithNoTrip()
        {
            var session = MakeSession(tripId: null);
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(true);

            SetupAiReply("TRIP_UPDATE_FLIGHT:{}");

            var result = await _service.SendMessageAsync(session.Id, "غيرلي الطيران");

            Assert.Contains("مفيش رحلة", result.Message);
        }

        // ============================================================
        // SendMessageAsync — TRIP_UPDATE_FLIGHT with existing trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldRegenerateFlight_WhenTripExists()
        {
            var tripId = Guid.NewGuid();
            var session = MakeSession(tripId: tripId);
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(true);
            _usageLimitMock.Setup(u => u.IncrementMessageUsageAsync(session.UserId)).Returns(Task.CompletedTask);
            _chatRepoMock.Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
            _chatRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            _orchestratorMock.Setup(o => o.RegenerateFlightAsync(tripId))
                .ReturnsAsync(new TripFlightDto { AirlineName = "EgyptAir", FlightNumber = "MS700" });
            _orchestratorMock.Setup(o => o.GetCurrentPlanAsync(tripId))
                .ReturnsAsync(new TripPlanDto());

            SetupAiReply("TRIP_UPDATE_FLIGHT:{}");

            var result = await _service.SendMessageAsync(session.Id, "غيرلي الطيران");

            Assert.Contains("EgyptAir", result.Message);
            _orchestratorMock.Verify(o => o.RegenerateFlightAsync(tripId), Times.Once);
        }

        // ============================================================
        // SendMessageAsync — TRIP_SHOW with no trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldReturnNoTrip_WhenShowWithNoTrip()
        {
            var session = MakeSession(tripId: null);
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(true);

            SetupAiReply("TRIP_SHOW:{}");

            var result = await _service.SendMessageAsync(session.Id, "ابعت الرحلة");

            Assert.Contains("مفيش رحلة", result.Message);
        }

        // ============================================================
        // SendMessageAsync — TRIP_UPDATE_ACTIVITIES with no trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldReturnNoTrip_WhenActivitiesUpdateWithNoTrip()
        {
            var session = MakeSession(tripId: null);
            _chatRepoMock.Setup(r => r.GetSessionAsync(session.Id)).ReturnsAsync(session);
            _usageLimitMock.Setup(u => u.CanSendMessageAsync(session.UserId)).ReturnsAsync(true);

            SetupAiReply("TRIP_UPDATE_ACTIVITIES:{\"dayNumber\": 1}");

            var result = await _service.SendMessageAsync(session.Id, "غيرلي أنشطة يوم 1");

            Assert.Contains("مفيش رحلة", result.Message);
        }

        // ============================================================
        // CreateSessionAsync
        // ============================================================

        [Fact]
        public async Task CreateSessionAsync_ShouldReturnSession()
        {
            var session = MakeSession();
            _chatRepoMock.Setup(r => r.CreateSessionAsync("user-1")).ReturnsAsync(session);
            _chatRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var result = await _service.CreateSessionAsync("user-1");

            Assert.NotNull(result);
            Assert.Equal("user-1", result.UserId);
        }

        // ============================================================
        // GetHistoryAsync
        // ============================================================

        [Fact]
        public async Task GetHistoryAsync_ShouldReturnMessages()
        {
            var sessionId = Guid.NewGuid();
            var messages = new List<ChatMessage>
            {
                new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.User, Content = "مرحبا" }
            };
            _chatRepoMock.Setup(r => r.GetMessagesAsync(sessionId)).ReturnsAsync(messages);

            var result = await _service.GetHistoryAsync(sessionId);

            Assert.Single(result);
            Assert.Equal("مرحبا", result[0].Content);
        }
    }
}