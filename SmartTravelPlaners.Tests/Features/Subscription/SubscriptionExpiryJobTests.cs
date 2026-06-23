using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Subscription.Services;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Payment
{
    public class SubscriptionExpiryJobTests
    {
        [Fact]
        public async Task DoWork_ShouldExpireSubscriptions_WhenExpiredExists()
        {
            // Arrange
            var expiredSubs = new List<Subscription>
            {
                new Subscription
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = Guid.NewGuid(),
                    Status = SubscriptionStatus.Active,
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1)
                }
            };

            var repoMock = new Mock<IGenericRepository<Subscription>>();
            repoMock
                .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>()))
                .ReturnsAsync(expiredSubs);

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock
                .Setup(u => u.Repository<Subscription>())
                .Returns(repoMock.Object);

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(s => s.GetService(typeof(IUnitOfWork)))
                .Returns(unitOfWorkMock.Object);

            var scopeMock = new Mock<IServiceScope>();
            scopeMock
                .Setup(s => s.ServiceProvider)
                .Returns(serviceProviderMock.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock
                .Setup(f => f.CreateScope())
                .Returns(scopeMock.Object);

            var loggerMock = new Mock<ILogger<SubscriptionExpiryJob>>();

            var job = new SubscriptionExpiryJob(
                scopeFactoryMock.Object,
                loggerMock.Object);

            // Act (استدعاء الميثود private بالـ Reflection)
            var method = typeof(SubscriptionExpiryJob)
                .GetMethod("DoWork", BindingFlags.NonPublic | BindingFlags.Instance);

            method.Invoke(job, new object?[] { null });

            await Task.Delay(200);

            // Assert
            Assert.Equal(SubscriptionStatus.Expired, expiredSubs[0].Status);

            unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
        }
    }
}