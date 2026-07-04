using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.DTOs;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Services;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Linq.Expressions;
using Xunit;

namespace SmartTravelPlaners.Tests.FeaturesTests.Trips
{
    public class TripCreationServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<IUsageLimitService> _usageLimitMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly TripCreationService _service;

        public TripCreationServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _usageLimitMock = new Mock<IUsageLimitService>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            var orchestratorMock = new Mock<ITripOrchestratorService>();

            scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            _serviceProviderMock
                .Setup(s => s.GetService(typeof(IServiceScopeFactory)))
                .Returns(scopeFactoryMock.Object);
            _serviceProviderMock
                .Setup(s => s.GetService(typeof(ITripOrchestratorService)))
                .Returns(orchestratorMock.Object);
            _serviceProviderMock
                .Setup(s => s.GetService(typeof(IUsageLimitService)))
                .Returns(_usageLimitMock.Object);

            var chatRepoMock = new Mock<IChatRepository>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<TripCreationService>>();

            _service = new TripCreationService(
                _uowMock.Object,
                _usageLimitMock.Object,
                _serviceProviderMock.Object,
                chatRepoMock.Object,
                loggerMock.Object);
        }

        private TripCreateDto MakeDto() => new TripCreateDto
        {
            Destination = "Paris",
            OriginCity = "Cairo",
            StartDate = "2025-06-01",
            EndDate = "2025-06-07",
            NumTravelers = 2,
            BudgetTotal = 3000,
            Preferences = new List<string> { "Culture", "Food" }
        };

        private void SetupUserProfile(string userId = "user-1")
        {
            var userProfileRepoMock = new Mock<IUserProfileRepository>();
            userProfileRepoMock
                .Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserProfile, bool>>>()))
                .ReturnsAsync(new List<UserProfile>
                {
                    new UserProfile { Id = Guid.NewGuid(), AspNetUserId = userId }
                });

            _uowMock.Setup(u => u.UserProfiles).Returns(userProfileRepoMock.Object);

            var tripRepoMock = new Mock<ITripRepository>();
            tripRepoMock.Setup(r => r.AddAsync(It.IsAny<Trip>())).Returns(Task.CompletedTask);
            _uowMock.Setup(u => u.Trips).Returns(tripRepoMock.Object);
            _uowMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
        }

        // ============================================================
        // CreateAndBuildAsync - Limit Reached
        // ============================================================

        [Fact]
        public async Task CreateAndBuildAsync_ShouldReturnLimitReached_WhenTripLimitExceeded()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(false);

            var result = await _service.CreateAndBuildAsync(MakeDto(), "user-1");

            Assert.True(result.LimitReached);
            Assert.Equal(TripCreationService.LimitReachedMessage, result.Message);
            Assert.Null(result.Trip);
        }

        [Fact]
        public async Task CreateAndBuildAsync_ShouldNotCreateTrip_WhenLimitReached()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(false);

            await _service.CreateAndBuildAsync(MakeDto(), "user-1");

            _uowMock.Verify(u => u.CompleteAsync(), Times.Never);
        }

        // ============================================================
        // CreateAndBuildAsync - Success
        // ============================================================

        [Fact]
        public async Task CreateAndBuildAsync_ShouldReturnTrip_WhenSuccess()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            SetupUserProfile("user-1");

            var result = await _service.CreateAndBuildAsync(MakeDto(), "user-1");

            Assert.False(result.LimitReached);
            Assert.NotNull(result.Trip);
            Assert.Equal("Paris", result.Trip!.Destination);
        }

        [Fact]
        public async Task CreateAndBuildAsync_ShouldPersistTrip_WhenSuccess()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            SetupUserProfile("user-1");

            await _service.CreateAndBuildAsync(MakeDto(), "user-1");

            _uowMock.Verify(u => u.CompleteAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateAndBuildAsync_ShouldSetCorrectTripFields_WhenSuccess()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            SetupUserProfile("user-1");

            var dto = MakeDto();
            var result = await _service.CreateAndBuildAsync(dto, "user-1");

            var trip = result.Trip!;
            Assert.Equal("Paris", trip.Destination);
            Assert.Equal("Cairo", trip.OriginCity);
            Assert.Equal(DateOnly.Parse("2025-06-01"), trip.StartDate);
            Assert.Equal(DateOnly.Parse("2025-06-07"), trip.EndDate);
            Assert.Equal(2, trip.NumTravelers);
            Assert.Equal(3000, trip.BudgetTotal);
        }

        [Fact]
        public async Task CreateAndBuildAsync_ShouldSetTripTitle_WhenSuccess()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            SetupUserProfile("user-1");

            var result = await _service.CreateAndBuildAsync(MakeDto(), "user-1");

            Assert.Equal("Trip to Paris", result.Trip!.Title);
        }

        [Fact]
        public async Task CreateAndBuildAsync_ShouldMapPreferences_WhenSuccess()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            SetupUserProfile("user-1");

            var result = await _service.CreateAndBuildAsync(MakeDto(), "user-1");

            Assert.Equal(2, result.Trip!.Preferences.Count);
            Assert.Contains(result.Trip.Preferences, p => p.Value == "Culture");
            Assert.Contains(result.Trip.Preferences, p => p.Value == "Food");
        }

        // ============================================================
        // CreateAndBuildAsync - User Profile Not Found
        // ============================================================

        [Fact]
        public async Task CreateAndBuildAsync_ShouldThrow_WhenUserProfileNotFound()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            var userProfileRepoMock = new Mock<IUserProfileRepository>();
            userProfileRepoMock
                .Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserProfile, bool>>>()))
                .ReturnsAsync(new List<UserProfile>());

            _uowMock.Setup(u => u.UserProfiles).Returns(userProfileRepoMock.Object);

            await Assert.ThrowsAsync<Exception>(() =>
                _service.CreateAndBuildAsync(MakeDto(), "user-1"));
        }

        // ============================================================
        // CreateAndBuildAsync - Invalid Dates
        // ============================================================

        [Fact]
        public async Task CreateAndBuildAsync_ShouldThrow_WhenStartDateInvalid()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            SetupUserProfile("user-1");

            var dto = MakeDto();
            dto.StartDate = "not-a-date";

            await Assert.ThrowsAsync<Exception>(() =>
                _service.CreateAndBuildAsync(dto, "user-1"));
        }

        [Fact]
        public async Task CreateAndBuildAsync_ShouldThrow_WhenEndDateInvalid()
        {
            _usageLimitMock
                .Setup(u => u.CanGenerateTripAsync("user-1"))
                .ReturnsAsync(true);

            SetupUserProfile("user-1");

            var dto = MakeDto();
            dto.EndDate = "not-a-date";

            await Assert.ThrowsAsync<Exception>(() =>
                _service.CreateAndBuildAsync(dto, "user-1"));
        }
    }
}