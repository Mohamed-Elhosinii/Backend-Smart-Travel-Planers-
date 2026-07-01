using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Services;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
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
        private readonly Mock<IServiceProvider> _serviceProviderMock;
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
            _serviceProviderMock = new Mock<IServiceProvider>();
            var _configMock = new Mock<IConfiguration>();
            var _tripCreationMock = new Mock<ITripCreationService>();

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();

            scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
            _serviceProviderMock.Setup(s => s.GetService(typeof(IServiceScopeFactory)))
                .Returns(scopeFactoryMock.Object);
            _serviceProviderMock.Setup(s => s.GetService(typeof(ITripOrchestratorService)))
                .Returns(_orchestratorMock.Object);
            _serviceProviderMock.Setup(s => s.GetService(typeof(IUsageLimitService)))
                .Returns(_usageLimitMock.Object);

            _configMock.Setup(c => c["GitHubModels:Token"]).Returns("fake-api-key");
            _configMock.Setup(c => c["GitHubModels:Endpoint"]).Returns("https://fake-endpoint.com");
            _configMock.Setup(c => c["GitHubModels:ModelId"]).Returns("fake-model");

            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(_aiMock.Object);
            var builtKernel = kernelBuilder.Build();

            var weatherPlugin = new SmartTravelPlaners.BLL.Features.Weather.Plugins.WeatherPlugin(new Mock<SmartTravelPlaners.BLL.Features.Weather.Interfaces.IWeatherApiService>().Object);
            var hotelPlugin = new SmartTravelPlaners.BLL.Features.Hotel.Plugins.HotelPlugin(
                new Mock<SmartTravelPlaners.BLL.Features.Hotel.Interfaces.IHotelApiService>().Object,
                new Mock<SmartTravelPlaners.BLL.Features.Hotel.Interfaces.IPlaceResolverService>().Object,
                new Mock<SmartTravelPlaners.BLL.Features.Hotel.Interfaces.IHotelSearchService>().Object);
            var flightPlugin = new SmartTravelPlaners.BLL.Features.Flight.Plugins.FlightPlugin(new Mock<SmartTravelPlaners.BLL.Features.Flight.Interfaces.IFlightService>().Object);
            var placesPlugin = new SmartTravelPlaners.BLL.Features.Place.Plugins.PlacesPlugin(new Mock<SmartTravelPlaners.BLL.Features.Place.Interfaces.IPlacesApiService>().Object, new Mock<Microsoft.Extensions.Logging.ILogger<SmartTravelPlaners.BLL.Features.Place.Plugins.PlacesPlugin>>().Object);
            var tripPlugin = new SmartTravelPlaners.BLL.Features.Trips.Plugins.TripPlugin(_tripCreationMock.Object, _tripRepoMock.Object, _uowMock.Object, _serviceProviderMock.Object);

            _service = new ChatService(
                _chatRepoMock.Object,
                _tripRepoMock.Object,
                _userProfileRepoMock.Object,
                _orchestratorMock.Object,
                _usageLimitMock.Object,
                builtKernel,
                tripPlugin,
                flightPlugin,
                hotelPlugin,
                placesPlugin,
                weatherPlugin);
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

            await Assert.ThrowsAsync<Exception>(() => _service.SendMessageAsync(sessionId, "user-1", "مرحبا"));
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

            var result = await _service.SendMessageAsync(session.Id, session.UserId, "مرحبا");

            Assert.Contains("limit", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ============================================================
        // SendMessageAsync — Normal Reply
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

            var result = await _service.SendMessageAsync(session.Id, session.UserId, "مرحبا");

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

            var result = await _service.SendMessageAsync(session.Id, session.UserId,"غيرلي الفندق");

            Assert.Contains("مفيش رحلة", result.Message);
        }

        // ============================================================
        // SendMessageAsync — TRIP_UPDATE_HOTEL with existing trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldRegenerateHotel_WhenTripExists()
        {
            // Arrange
            var tripId = Guid.NewGuid();

            var trip = new Trip
            {
                Id = tripId,
                Destination = "Rome",
                OriginCity = "Cairo",
                StartDate = new DateOnly(2026, 7, 4),
                EndDate = new DateOnly(2026, 7, 8),
                NumTravelers = 2,
                BudgetTotal = 5000
            };

            var session = MakeSession(tripId: tripId);

            _chatRepoMock
                .Setup(r => r.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            _tripRepoMock
                .Setup(r => r.GetByIdAsync(tripId))
                .ReturnsAsync(trip);

            _usageLimitMock
                .Setup(u => u.CanSendMessageAsync(session.UserId))
                .ReturnsAsync(true);

            _usageLimitMock
                .Setup(u => u.IncrementMessageUsageAsync(session.UserId))
                .Returns(Task.CompletedTask);

            _chatRepoMock
                .Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);

            _chatRepoMock
                .Setup(r => r.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            SetupAiReply("TRIP_UPDATE_HOTEL:{}");

            // Act
            var result = await _service.SendMessageAsync(
                session.Id,
                session.UserId,
                "غيرلي الفندق");

            // Assert
            Assert.Contains("جاري", result.Message);
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

            var result = await _service.SendMessageAsync(session.Id, session.UserId,  "غيرلي الطيران");

            Assert.Contains("مفيش رحلة", result.Message);
        }

        // ============================================================
        // SendMessageAsync — TRIP_UPDATE_FLIGHT with existing trip
        // ============================================================

        [Fact]
        public async Task SendMessageAsync_ShouldRegenerateFlight_WhenTripExists()
        {
            // Arrange
            var tripId = Guid.NewGuid();

            var trip = new Trip
            {
                Id = tripId,
                Destination = "Rome",
                OriginCity = "Cairo",
                StartDate = new DateOnly(2026, 7, 4),
                EndDate = new DateOnly(2026, 7, 8),
                NumTravelers = 2,
                BudgetTotal = 5000
            };

            var session = MakeSession(tripId: tripId);

            _chatRepoMock
                .Setup(r => r.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            _tripRepoMock
                .Setup(r => r.GetByIdAsync(tripId))
                .ReturnsAsync(trip);

            _usageLimitMock
                .Setup(x => x.CanSendMessageAsync(session.UserId))
                .ReturnsAsync(true);

            _usageLimitMock
                .Setup(x => x.IncrementMessageUsageAsync(session.UserId))
                .Returns(Task.CompletedTask);

            _chatRepoMock
                .Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);

            _chatRepoMock
                .Setup(x => x.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            SetupAiReply("TRIP_UPDATE_FLIGHT:{}");

            // Act
            var result = await _service.SendMessageAsync(
                session.Id,
                session.UserId,
                "غيرلي الطيران");

            // Assert
            Assert.Contains("جاري", result.Message);
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

            var result = await _service.SendMessageAsync(session.Id, session.UserId,"ابعت الرحلة");

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

            var result = await _service.SendMessageAsync(session.Id, session.UserId,  "غيرلي أنشطة يوم 1");

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
            var session = new ChatSession { Id = sessionId, UserId = "user-1" };
            var messages = new List<ChatMessage>
    {
        new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.User, Content = "مرحبا" }
    };

            _chatRepoMock.Setup(r => r.GetSessionAsync(sessionId)).ReturnsAsync(session);
            _chatRepoMock.Setup(r => r.GetMessagesAsync(sessionId)).ReturnsAsync(messages);

            var result = await _service.GetHistoryAsync(sessionId, "user-1");

            Assert.Single(result);
            Assert.Equal("مرحبا", result[0].Content);
        }

        // ============================================================
        // GetTripPlanAsync
        // ============================================================

        [Fact]
        public async Task GetTripPlanAsync_ShouldReturnPlan_WhenExists()
        {
            var tripId = Guid.NewGuid();
            var userId = "user-123";
            var profileId = Guid.NewGuid();

            _userProfileRepoMock.Setup(repo => repo.GetUserProfileWithPreferencesAsync(userId))
                .ReturnsAsync(new UserProfile { Id = profileId, AspNetUserId = userId });

            _tripRepoMock.Setup(repo => repo.GetByIdAsync(tripId))
                .ReturnsAsync(new Trip { Id = tripId, UserId = profileId });

            _orchestratorMock.Setup(o => o.GetCurrentPlanAsync(tripId))
                .ReturnsAsync(new TripPlanDto { TripId = tripId, Destination = "Paris" });

            var result = await _service.GetTripPlanAsync(tripId, userId);

            Assert.NotNull(result);
            Assert.Equal("Paris", result!.Destination);
        }

        [Fact]
        public async Task GetTripPlanAsync_ShouldReturnNull_WhenThrows()
        {
            var tripId = Guid.NewGuid();
            var userId = "user-123";
            var profileId = Guid.NewGuid();

            _userProfileRepoMock.Setup(repo => repo.GetUserProfileWithPreferencesAsync(userId))
                .ReturnsAsync(new UserProfile { Id = profileId, AspNetUserId = userId });

            _tripRepoMock.Setup(repo => repo.GetByIdAsync(tripId))
                .ReturnsAsync(new Trip { Id = tripId, UserId = profileId });

            _orchestratorMock.Setup(o => o.GetCurrentPlanAsync(tripId))
                .ThrowsAsync(new Exception("Not found"));

            var result = await _service.GetTripPlanAsync(tripId, userId);

            Assert.Null(result);
        }
    }
}