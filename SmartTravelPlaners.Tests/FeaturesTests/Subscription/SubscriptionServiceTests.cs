using Moq;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Services;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Linq.Expressions;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Payment
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<IPaymobService> _paymobMock;
        private readonly Mock<IUsageLimitService> _usageMock;

        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _paymobMock = new Mock<IPaymobService>();
            _usageMock = new Mock<IUsageLimitService>();

            _service = new SubscriptionService(
                _uowMock.Object,
                _paymobMock.Object,
                _usageMock.Object);
        }
        // ============================================================
        // GetPlansAsync
        // ============================================================
        [Fact]
        public async Task GetPlansAsync_ShouldReturnPlans()
        {
            // Arrange
            var plans = new List<Plan>
    {
        new Plan { Id = Guid.NewGuid(), Name = "Basic", PriceMonthly = 10 }
    };

            var repoMock = new Mock<IGenericRepository<Plan>>();
            repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(plans);

            _uowMock.Setup(u => u.Repository<Plan>())
                .Returns(repoMock.Object);

            // Act
            var result = await _service.GetPlansAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("Basic", result.First().Name);
        }
        // ============================================================
        // CreateSubscriptionAsync (Free plan)
        // ============================================================
        [Fact]
        public async Task CreateSubscriptionAsync_ShouldReturnNull_ForFreePlan()
        {
            // Arrange
            var planId = Guid.NewGuid();

            var plan = new Plan
            {
                Id = planId,
                PriceMonthly = 0
            };

            var planRepo = new Mock<IGenericRepository<Plan>>();
            planRepo.Setup(r => r.GetByIdAsync(planId))
                .ReturnsAsync(plan);

            var subRepo = new Mock<IGenericRepository<Subscription>>();
            subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>()))
                .ReturnsAsync(new List<Subscription>());

            var userProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user1"
            };

            _uowMock.Setup(u => u.Repository<Plan>()).Returns(planRepo.Object);
            _uowMock.Setup(u => u.Repository<Subscription>()).Returns(subRepo.Object);
            _uowMock.Setup(u => u.UserProfiles.FindAsync(It.IsAny<Expression<Func<UserProfile, bool>>>()))
                .ReturnsAsync(new List<UserProfile> { userProfile });

            // Act
            var result = await _service.CreateSubscriptionAsync("user1", planId);

            // Assert
            Assert.Null(result);
            _uowMock.Verify(u => u.CompleteAsync(), Times.Once);
        }
        // ============================================================
        // CreateSubscriptionAsync(Paid Plan)
        // ============================================================
        [Fact]
        public async Task CreateSubscriptionAsync_ShouldReturnPaymentUrl_ForPaidPlan()
        {
            // Arrange
            var planId = Guid.NewGuid();

            var plan = new Plan
            {
                Id = planId,
                PriceMonthly = 100
            };

            var planRepo = new Mock<IGenericRepository<Plan>>();
            planRepo.Setup(r => r.GetByIdAsync(planId))
                .ReturnsAsync(plan);

            var subRepo = new Mock<IGenericRepository<Subscription>>();
            subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>()))
                .ReturnsAsync(new List<Subscription>());

            var paymentRepo = new Mock<IGenericRepository<PaymentTransaction>>();

            var userProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user1"
            };

            _uowMock.Setup(u => u.Repository<Plan>()).Returns(planRepo.Object);
            _uowMock.Setup(u => u.Repository<Subscription>()).Returns(subRepo.Object);
            _uowMock.Setup(u => u.Repository<PaymentTransaction>()).Returns(paymentRepo.Object);

            _uowMock.Setup(u => u.UserProfiles.FindAsync(It.IsAny<Expression<Func<UserProfile, bool>>>()))
                .ReturnsAsync(new List<UserProfile> { userProfile });

            _paymobMock.Setup(p => p.InitiatePaymentAsync(
                It.IsAny<string>(),
                It.IsAny<Plan>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()))
                .ReturnsAsync("payment-url");

            // Act
            var result = await _service.CreateSubscriptionAsync("user1", planId);

            // Assert
            Assert.Equal("payment-url", result);
        }
        // ============================================================
        // CancelSubscriptionAsync(Refund)
        // ============================================================
        [Fact]
        public async Task CancelSubscriptionAsync_ShouldRefundAndCancel()
        {
            // Arrange
            var userProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user1"
            };

            var sub = new Subscription
            {
                Id = Guid.NewGuid(),
                Status = SubscriptionStatus.Active,
                UserProfileId = userProfile.Id
            };

            var transaction = new PaymentTransaction
            {
                SubscriptionId = sub.Id,
                Status = "paid",
                PaymobTransactionId = "123",
                Amount = 100
            };

            var subRepo = new Mock<IGenericRepository<Subscription>>();
            subRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>()))
                .ReturnsAsync(new List<Subscription> { sub });

            var paymentRepo = new Mock<IGenericRepository<PaymentTransaction>>();
            paymentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>()))
                .ReturnsAsync(new List<PaymentTransaction> { transaction });

            _uowMock.Setup(u => u.Repository<Subscription>()).Returns(subRepo.Object);
            _uowMock.Setup(u => u.Repository<PaymentTransaction>()).Returns(paymentRepo.Object);

            _uowMock.Setup(u => u.UserProfiles.FindAsync(It.IsAny<Expression<Func<UserProfile, bool>>>()))
                .ReturnsAsync(new List<UserProfile> { userProfile });

            _paymobMock.Setup(p => p.RefundPaymentAsync("123", 100))
                .ReturnsAsync(true);

            // Act
            await _service.CancelSubscriptionAsync("user1");

            // Assert
            Assert.Equal(SubscriptionStatus.Cancelled, sub.Status);
            Assert.Equal("refunded", transaction.Status);
        }
    }
}