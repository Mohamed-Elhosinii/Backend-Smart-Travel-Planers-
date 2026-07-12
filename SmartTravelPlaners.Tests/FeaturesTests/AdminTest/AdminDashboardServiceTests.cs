using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Admin.Services;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Admin
{
    public class AdminDashboardServiceTests
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<ILogger<AdminDashboardService>> _loggerMock;
        private readonly AdminDashboardService _service;

        public AdminDashboardServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
            _loggerMock = new Mock<ILogger<AdminDashboardService>>();

            SeedData();

            _service = new AdminDashboardService(_context, _userManagerMock.Object, _loggerMock.Object);
        }

        private void SeedData()
        {
            var user = new ApplicationUser
            {
                Id = "user1",
                Email = "test@example.com",
                FullName = "Test User"
            };

            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = "user1",
                CreatedAt = DateTime.UtcNow
            };

            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "Basic",
                PriceMonthly = 100,
                MaxTripsPerMonth = 5,
                MaxMessagesPerMonth = 100
            };

            var subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                UserProfileId = profile.Id,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active,
                CurrentPeriodStart = DateTime.UtcNow
            };

            var transaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                Amount = 200,
                Status = "paid",
                CreatedAt = DateTime.UtcNow,
                Subscription = subscription
            };

            _context.Users.Add(user);
            _context.UserProfiles.Add(profile);
            _context.Plans.Add(plan);
            _context.Subscriptions.Add(subscription);
            _context.PaymentTransactions.Add(transaction);
            _context.SaveChanges();
        }

        // ======================================================
        // OVERVIEW STATS
        // ======================================================

        [Fact]
        public async Task GetOverviewStatsAsync_ShouldReturnStats()
        {
            var result = await _service.GetOverviewStatsAsync();

            Assert.NotNull(result);
            Assert.True(result.TotalUsers > 0);
            Assert.True(result.TotalTrips >= 0);
            Assert.True(result.TotalRevenue > 0);
            Assert.NotNull(result.UserRegistrations);
        }

        // ======================================================
        // USERS LIST
        // ======================================================

        [Fact]
        public async Task GetUsersListAsync_ShouldReturnUsers()
        {
            var (users, total) = await _service.GetUsersListAsync(null, 1, 10);

            Assert.NotEmpty(users);
            Assert.True(total > 0);
        }

        [Fact]
        public async Task GetUsersListAsync_ShouldFilterBySearch()
        {
            var (users, _) = await _service.GetUsersListAsync("test", 1, 10);

            Assert.All(users, u =>
                Assert.Contains("test", u.Email.ToLower()));
        }

        // ======================================================
        // UPDATE SUBSCRIPTION
        // ======================================================

        [Fact]
        public async Task UpdateUserSubscriptionPlanAsync_ShouldChangePlan()
        {
            // Arrange
            var userId = "user_update_1";

            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                AspNetUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            var oldPlan = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "Basic",
                PriceMonthly = 100
            };

            var newPlan = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "Pro",
                PriceMonthly = 300
            };

            var subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                UserProfileId = profile.Id,
                PlanId = oldPlan.Id,
                Status = SubscriptionStatus.Active,
                CurrentPeriodStart = DateTime.UtcNow
            };

            _context.UserProfiles.Add(profile);
            _context.Plans.AddRange(oldPlan, newPlan);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

         
            await _service.UpdateUserSubscriptionPlanAsync(userId, newPlan.Id);

           
            var originalSubscription = await _context.Subscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == subscription.Id);

            var activeSubscription = await _context.Subscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserProfileId == profile.Id && s.Status == SubscriptionStatus.Active);

          
            Assert.NotNull(originalSubscription);
            Assert.Equal(SubscriptionStatus.Cancelled, originalSubscription!.Status);
            Assert.NotNull(activeSubscription);
            Assert.Equal(newPlan.Id, activeSubscription!.PlanId);
            Assert.Equal(SubscriptionStatus.Active, activeSubscription.Status);
        }

        // ======================================================
        // TOGGLE USER STATUS
        // ======================================================

        [Fact]
        public async Task ToggleUserStatusAsync_ShouldBanAndUnbanUser()
        {
            var user = new ApplicationUser { Id = "u2", Email = "x@test.com" };

            _userManagerMock.Setup(u => u.FindByIdAsync("u2"))
                .ReturnsAsync(user);

            _userManagerMock.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            await _service.ToggleUserStatusAsync("u2");

            Assert.NotNull(user.LockoutEnd);

            await _service.ToggleUserStatusAsync("u2");

            Assert.Null(user.LockoutEnd);
        }

        // ======================================================
        // PAYMENTS HISTORY
        // ======================================================

        [Fact]
        public async Task GetPaymentsHistoryAsync_ShouldReturnTransactions()
        {
            var (list, total) = await _service.GetPaymentsHistoryAsync(1, 10);

            Assert.NotEmpty(list);
            Assert.True(total > 0);
        }

        // ======================================================
        // PLANS
        // ======================================================

        [Fact]
        public async Task CreatePlanAsync_ShouldAddPlan()
        {
            var dto = new PlanDto
            {
                Name = "Gold",
                PriceMonthly = 500,
                MaxTripsPerMonth = 30,
                MaxMessagesPerMonth = 1000
            };

            var result = await _service.CreatePlanAsync(dto);

            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Gold", _context.Plans.Last().Name);
        }

        [Fact]
        public async Task UpdatePlanAsync_ShouldModifyPlan()
        {
            var plan = _context.Plans.First();

            var dto = new PlanDto
            {
                Name = "Updated",
                PriceMonthly = 999,
                MaxTripsPerMonth = 99,
                MaxMessagesPerMonth = 999
            };

            await _service.UpdatePlanAsync(plan.Id, dto);

            var updated = _context.Plans.Find(plan.Id);

            Assert.Equal("Updated", updated!.Name);
        }

        [Fact]
        public async Task DeletePlanAsync_ShouldDelete_WhenNoActiveSubs()
        {
            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                Name = "Temp",
                PriceMonthly = 10
            };

            _context.Plans.Add(plan);
            await _context.SaveChangesAsync();

            await _service.DeletePlanAsync(plan.Id);

            Assert.Null(_context.Plans.Find(plan.Id));
        }
    }
}