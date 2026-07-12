using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Chat.Services;
using SmartTravelPlaners.BLL.Features.Flight.Plugins;
using SmartTravelPlaners.BLL.Features.Hotel.Plugins;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Place.Plugins;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Plugins;
using SmartTravelPlaners.BLL.Features.Weather.Plugins;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly Mock<ILogger<ChatService>> _loggerMock;
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
            _loggerMock = new Mock<ILogger<ChatService>>();

            // Build a Kernel and register the mocked AI service
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(_aiMock.Object);
            var kernel = kernelBuilder.Build();

            // Create plugin instances with simple mocked dependencies (avoid mocking TripPlugin itself)
            var tripCreationMock = new Mock<ITripCreationService>().Object;
            var tripPlugin = new TripPlugin(tripCreationMock, _tripRepoMock.Object, _uowMock.Object, _serviceProviderMock.Object);

            var flightPlugin = new FlightPlugin(new Mock<SmartTravelPlaners.BLL.Features.Flight.Interfaces.IFlightService>().Object);
            var hotelPlugin = new HotelPlugin(new Mock<SmartTravelPlaners.BLL.Features.Hotel.Interfaces.IHotelApiService>().Object);
            var placesPlugin = new PlacesPlugin(new Mock<SmartTravelPlaners.BLL.Features.Place.Interfaces.IPlacesApiService>().Object, new Mock<ILogger<PlacesPlugin>>().Object);
            var weatherPlugin = new WeatherPlugin(new Mock<SmartTravelPlaners.BLL.Features.Weather.Interfaces.IWeatherApiService>().Object);

            _service = new ChatService(
                _chatRepoMock.Object,
                _tripRepoMock.Object,
                _userProfileRepoMock.Object,
                _orchestratorMock.Object,
                _usageLimitMock.Object,
                kernel,
                tripPlugin,
                flightPlugin,
                hotelPlugin,
                placesPlugin,
                weatherPlugin,
                _loggerMock.Object);
        }

        private ChatSession MakeSession(Guid? tripId = null) => new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            TripId = tripId,
            Messages = new List<ChatMessage>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        

        [Fact]
        public async System.Threading.Tasks.Task SendMessageAsync_ShouldThrow_WhenSessionNotFound()
        {
            var sessionId = Guid.NewGuid();
            _chatRepoMock.Setup(r => r.GetSessionAsync(sessionId)).ReturnsAsync((ChatSession?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.SendMessageAsync(sessionId, "user-1", "مرحبا"));
        }
        [Fact]
        public async Task SendMessageAsync_ShouldThrow_WhenUserIsNotOwner()
        {
            var session = MakeSession();
            session.UserId = "another-user";

            _chatRepoMock
                .Setup(r => r.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.SendMessageAsync(session.Id, "user-1", "Hello"));
        }

        [Fact]
        public async Task SendMessageAsync_ShouldReturnLimitMessage_WhenUsageLimitReached()
        {
            var session = MakeSession();

            _chatRepoMock
                .Setup(r => r.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            _usageLimitMock
                .Setup(u => u.CanSendMessageAsync(session.UserId))
                .ReturnsAsync(false);

            var result = await _service.SendMessageAsync(
                session.Id,
                session.UserId,
                "Hello");

            Assert.NotNull(result);
            Assert.Contains("monthly message limit", result.Message);

            _usageLimitMock.Verify(
                u => u.IncrementMessageUsageAsync(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateSessionAsync_ShouldCreateSession()
        {
            var session = MakeSession();

            _chatRepoMock
                .Setup(r => r.CreateSessionAsync("user-1"))
                .ReturnsAsync(session);

            var result = await _service.CreateSessionAsync("user-1");

            Assert.NotNull(result);

            _chatRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task GetUserSessionsAsync_ShouldReturnSessions()
        {
            var sessions = new List<ChatSession>
    {
        MakeSession(),
        MakeSession()
    };

            _chatRepoMock
                .Setup(r => r.GetSessionsByUserAsync("user-1"))
                .ReturnsAsync(sessions);

            var result = await _service.GetUserSessionsAsync("user-1");

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldThrow_WhenSessionNotFound()
        {
            var sessionId = Guid.NewGuid();

            _chatRepoMock
                .Setup(r => r.GetSessionAsync(sessionId))
                .ReturnsAsync((ChatSession?)null);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GetHistoryAsync(sessionId, "user-1"));
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldThrow_WhenUserDoesNotOwnSession()
        {
            var session = MakeSession();
            session.UserId = "another-user";

            _chatRepoMock
                .Setup(r => r.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GetHistoryAsync(session.Id, "user-1"));
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldReturnMessages()
        {
            var session = MakeSession();

            var messages = new List<ChatMessage>
    {
        new ChatMessage
        {
            Id = Guid.NewGuid(),
            Content = "Hello"
        }
    };

            _chatRepoMock
                .Setup(r => r.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            _chatRepoMock
                .Setup(r => r.GetMessagesAsync(session.Id))
                .ReturnsAsync(messages);

            var result = await _service.GetHistoryAsync(
                session.Id,
                session.UserId);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetTripPlanAsync_ShouldReturnNull_WhenProfileNotFound()
        {
            _userProfileRepoMock
                .Setup(x => x.GetUserProfileWithPreferencesAsync("user-1"))
                .ReturnsAsync((UserProfile?)null);

            var result = await _service.GetTripPlanAsync(
                Guid.NewGuid(),
                "user-1");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTripPlanAsync_ShouldReturnNull_WhenTripNotFound()
        {
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user-1"
            };

            _userProfileRepoMock
                .Setup(x => x.GetUserProfileWithPreferencesAsync("user-1"))
                .ReturnsAsync(profile);

            _tripRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Trip?)null);

            var result = await _service.GetTripPlanAsync(
                Guid.NewGuid(),
                "user-1");

            Assert.Null(result);
        }
        [Fact]
        public async Task LinkSessionToTripAsync_ShouldThrow_WhenSessionNotFound()
        {
            var sessionId = Guid.NewGuid();
            var tripId = Guid.NewGuid();

            _chatRepoMock
                .Setup(x => x.GetSessionAsync(sessionId))
                .ReturnsAsync((ChatSession?)null);

            await Assert.ThrowsAsync<Exception>(() =>
                _service.LinkSessionToTripAsync(sessionId, "user-1", tripId));
        }
        [Fact]
        public async Task LinkSessionToTripAsync_ShouldThrow_WhenUnauthorized()
        {
            var session = MakeSession();
            session.UserId = "another-user";

            _chatRepoMock
                .Setup(x => x.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.LinkSessionToTripAsync(session.Id, "user-1", Guid.NewGuid()));
        }
        [Fact]
        public async Task LinkSessionToTripAsync_ShouldLinkTrip()
        {
            var session = MakeSession();
            var tripId = Guid.NewGuid();

            _chatRepoMock
                .Setup(x => x.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            await _service.LinkSessionToTripAsync(
                session.Id,
                session.UserId,
                tripId);

            Assert.Equal(tripId, session.TripId);
            Assert.Equal(ChatStage.PlanReady, session.Stage);

            _chatRepoMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }
        [Fact]
        public async Task GetOrCreateTripSessionAsync_ShouldReturnExistingSession()
        {
            var session = MakeSession();
            var tripId = Guid.NewGuid();

            _chatRepoMock
                .Setup(x => x.GetSessionByTripIdAsync(tripId, "user-1"))
                .ReturnsAsync(session);

            var result = await _service.GetOrCreateTripSessionAsync(
                tripId,
                "user-1");

            Assert.Equal(session.Id, result.Id);

            _chatRepoMock.Verify(
                x => x.CreateSessionAsync(It.IsAny<string>()),
                Times.Never);
        }
        [Fact]
        public async Task GetOrCreateTripSessionAsync_ShouldCreateSession_WhenNotExists()
        {
            var tripId = Guid.NewGuid();
            var session = MakeSession();

            _chatRepoMock
                .Setup(x => x.GetSessionByTripIdAsync(tripId, "user-1"))
                .ReturnsAsync((ChatSession?)null);

            _chatRepoMock
                .Setup(x => x.CreateSessionAsync("user-1"))
                .ReturnsAsync(session);

            var result = await _service.GetOrCreateTripSessionAsync(
                tripId,
                "user-1");

            Assert.Equal(tripId, result.TripId);
            Assert.Equal(ChatStage.PlanReady, result.Stage);

            _chatRepoMock.Verify(
                x => x.SaveChangesAsync(),
                Times.Once);
        }
        // ============================================================
        // CreateSessionAsync - Exception
        // ============================================================

        [Fact]
        public async Task CreateSessionAsync_ShouldThrow_WhenRepositoryThrows()
        {
            _chatRepoMock
                .Setup(x => x.CreateSessionAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception());

            await Assert.ThrowsAsync<Exception>(() =>
                _service.CreateSessionAsync("user-1"));
        }

        // ============================================================
        // GetUserSessionsAsync - Exception
        // ============================================================

        [Fact]
        public async Task GetUserSessionsAsync_ShouldThrow_WhenRepositoryThrows()
        {
            _chatRepoMock
                .Setup(x => x.GetSessionsByUserAsync("user-1"))
                .ThrowsAsync(new Exception());

            await Assert.ThrowsAsync<Exception>(() =>
                _service.GetUserSessionsAsync("user-1"));
        }

        // ============================================================
        // GetHistoryAsync - Exception
        // ============================================================

        [Fact]
        public async Task GetHistoryAsync_ShouldThrow_WhenRepositoryThrows()
        {
            var session = MakeSession();

            _chatRepoMock
                .Setup(x => x.GetSessionAsync(session.Id))
                .ReturnsAsync(session);

            _chatRepoMock
                .Setup(x => x.GetMessagesAsync(session.Id))
                .ThrowsAsync(new Exception());

            await Assert.ThrowsAsync<Exception>(() =>
                _service.GetHistoryAsync(session.Id, session.UserId));
        }

        // ============================================================
        // GetTripPlanAsync - Unauthorized Trip
        // ============================================================

        [Fact]
        public async Task GetTripPlanAsync_ShouldReturnNull_WhenTripBelongsToAnotherUser()
        {
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user-1"
            };

            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid()
            };

            _userProfileRepoMock
                .Setup(x => x.GetUserProfileWithPreferencesAsync("user-1"))
                .ReturnsAsync(profile);

            _tripRepoMock
                .Setup(x => x.GetByIdAsync(trip.Id))
                .ReturnsAsync(trip);

            var result = await _service.GetTripPlanAsync(
                trip.Id,
                "user-1");

            Assert.Null(result);
        }

        // ============================================================
        // GetTripPlanAsync - Success
        // ============================================================

        [Fact]
        public async Task GetTripPlanAsync_ShouldReturnPlan()
        {
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user-1"
            };

            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                UserId = profile.Id
            };

            var plan = new TripPlanDto();

            _userProfileRepoMock
                .Setup(x => x.GetUserProfileWithPreferencesAsync("user-1"))
                .ReturnsAsync(profile);

            _tripRepoMock
                .Setup(x => x.GetByIdAsync(trip.Id))
                .ReturnsAsync(trip);

            _orchestratorMock
                .Setup(x => x.GetCurrentPlanAsync(trip.Id))
                .ReturnsAsync(plan);

            var result = await _service.GetTripPlanAsync(
                trip.Id,
                "user-1");

            Assert.NotNull(result);
            Assert.Equal(plan, result);
        }
    }
}
