using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Subscription.Services;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Linq.Expressions;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Payment
{
    public class UsageLimitServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock <ILogger<UsageLimitService>> _loggerMock;
        private readonly UsageLimitService _service;

        public UsageLimitServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _loggerMock = new Mock<ILogger<UsageLimitService>>();
            _service = new UsageLimitService(_uowMock.Object, _loggerMock.Object);
        }

        // ============================================================
        // CanGenerateTripAsync
        // ============================================================

        [Fact]
        public async Task CanGenerateTripAsync_ShouldReturnTrue_WhenUnderLimit()
        {
            SetupUser(tripsUsed: 1, tripsLimit: 5);

            var result = await _service.CanGenerateTripAsync("user1");

            Assert.True(result);
        }

        [Fact]
        public async Task CanGenerateTripAsync_ShouldReturnFalse_WhenLimitReached()
        {
            SetupUser(tripsUsed: 5, tripsLimit: 5);

            var result = await _service.CanGenerateTripAsync("user1");

            Assert.False(result);
        }

        // ============================================================
        // CanSendMessageAsync
        // ============================================================

        [Fact]
        public async Task CanSendMessageAsync_ShouldReturnTrue_WhenUnderLimit()
        {
            SetupUser(messagesUsed: 1, messagesLimit: 5);

            var result = await _service.CanSendMessageAsync("user1");

            Assert.True(result);
        }

        [Fact]
        public async Task CanSendMessageAsync_ShouldReturnFalse_WhenLimitReached()
        {
            SetupUser(messagesUsed: 5, messagesLimit: 5);

            var result = await _service.CanSendMessageAsync("user1");

            Assert.False(result);
        }

        // ============================================================
        // IncrementTripUsageAsync
        // ============================================================

        [Fact]
        public async Task IncrementTripUsageAsync_ShouldIncreaseTrips()
        {
            var counter = CreateCounter(trips: 1, messages: 0);

            SetupCounter(counter);

            await _service.IncrementTripUsageAsync("user1");

            Assert.Equal(2, counter.TripsUsed);
            _uowMock.Verify(u => u.CompleteAsync(), Times.Once);
        }

        // ============================================================
        // IncrementMessageUsageAsync
        // ============================================================

        [Fact]
        public async Task IncrementMessageUsageAsync_ShouldIncreaseMessages()
        {
            var counter = CreateCounter(trips: 0, messages: 2);

            SetupCounter(counter);

            await _service.IncrementMessageUsageAsync("user1");

            Assert.Equal(3, counter.MessagesUsed);
            _uowMock.Verify(u => u.CompleteAsync(), Times.Once);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void SetupUser(
            int tripsUsed = 0,
            int? tripsLimit = null,
            int messagesUsed = 0,
            int? messagesLimit = null)
        {
            var userProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user1"
            };

            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                MaxTripsPerMonth = tripsLimit,
                MaxMessagesPerMonth = messagesLimit
            };

            var subscription = new Subscription
            {
                UserProfileId = userProfile.Id,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active
            };

            var counter = new UsageCounter
            {
                UserProfileId = userProfile.Id,
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                TripsUsed = tripsUsed,
                MessagesUsed = messagesUsed
            };

            var userRepo = new Mock<IUserProfileRepository>();
            userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserProfile, bool>>>()))
                .ReturnsAsync(new List<UserProfile> { userProfile });

            var subRepo = new Mock<IGenericRepository<Subscription>>();
            subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>()))
                .ReturnsAsync(new List<Subscription> { subscription });

            var planRepo = new Mock<IGenericRepository<Plan>>();
            planRepo.Setup(r => r.GetByIdAsync(plan.Id))
                .ReturnsAsync(plan);

            var counterRepo = new Mock<IGenericRepository<UsageCounter>>();
            counterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UsageCounter, bool>>>()))
                .ReturnsAsync(new List<UsageCounter> { counter });

            _uowMock.Setup(u => u.UserProfiles).Returns(userRepo.Object);
            _uowMock.Setup(u => u.Repository<Subscription>()).Returns(subRepo.Object);
            _uowMock.Setup(u => u.Repository<Plan>()).Returns(planRepo.Object);
            _uowMock.Setup(u => u.Repository<UsageCounter>()).Returns(counterRepo.Object);
        }

        private void SetupCounter(UsageCounter counter)
        {
            var userProfile = new UserProfile
            {
                Id = counter.UserProfileId,
                AspNetUserId = "user1"
            };

            var userRepo = new Mock<IUserProfileRepository>();
            userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserProfile, bool>>>()))
                .ReturnsAsync(new List<UserProfile> { userProfile });

            var counterRepo = new Mock<IGenericRepository<UsageCounter>>();
            counterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UsageCounter, bool>>>()))
                .ReturnsAsync(new List<UsageCounter> { counter });

            _uowMock.Setup(u => u.UserProfiles).Returns(userRepo.Object);
            _uowMock.Setup(u => u.Repository<UsageCounter>()).Returns(counterRepo.Object);
        }

        private UsageCounter CreateCounter(int trips, int messages)
        {
            return new UsageCounter
            {
                Id = Guid.NewGuid(),
                UserProfileId = Guid.NewGuid(),
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                TripsUsed = trips,
                MessagesUsed = messages
            };
        }
    }
}