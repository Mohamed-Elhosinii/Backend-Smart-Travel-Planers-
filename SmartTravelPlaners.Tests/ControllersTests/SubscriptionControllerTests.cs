
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using System.Security.Claims;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class SubscriptionControllerTests
    {
        private readonly Mock<ISubscriptionService> _serviceMock;
        private readonly Mock<ILogger<SubscriptionController>> _loggerMock;
        private readonly SubscriptionController _controller;

        public SubscriptionControllerTests()
        {
            _serviceMock = new Mock<ISubscriptionService>();
            _loggerMock = new Mock<ILogger<SubscriptionController>>();
            _controller = new SubscriptionController(_serviceMock.Object, _loggerMock.Object);

        }

        private void SetupUser(string userId = "user-1")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
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
        // GetPlans
        // ============================================================

        [Fact]
        public async Task GetPlans_ShouldReturn200_WithPlans()
        {
            _serviceMock.Setup(s => s.GetPlansAsync())
                .ReturnsAsync(new List<PlanDto>
                {
                    new() { Id = Guid.NewGuid(), Name = "Free" },
                    new() { Id = Guid.NewGuid(), Name = "Pro" }
                });

            var result = await _controller.GetPlans() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // GetMySubscription
        // ============================================================

        [Fact]
        public async Task GetMySubscription_ShouldReturn401_WhenNoUser()
        {
            SetupNoUser();

            var result = await _controller.GetMySubscription() as UnauthorizedResult;

            Assert.NotNull(result);
            Assert.Equal(401, result!.StatusCode);
        }

        [Fact]
        public async Task GetMySubscription_ShouldReturn200_WhenUserExists()
        {
            SetupUser("user-1");
            _serviceMock.Setup(s => s.GetMySubscriptionAsync("user-1"))
                .ReturnsAsync(new SubscriptionDto { PlanName = "Free" });

            var result = await _controller.GetMySubscription() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // Subscribe
        // ============================================================

        [Fact]
        public async Task Subscribe_ShouldReturn401_WhenNoUser()
        {
            SetupNoUser();

            var result = await _controller.Subscribe(new CreateSubscriptionRequestDto { PlanId = Guid.NewGuid() }) as UnauthorizedResult;

            Assert.NotNull(result);
            Assert.Equal(401, result!.StatusCode);
        }

        [Fact]
        public async Task Subscribe_ShouldReturn200_WhenFreePlan()
        {
            SetupUser("user-1");
            var planId = Guid.NewGuid();
            _serviceMock.Setup(s => s.CreateSubscriptionAsync("user-1", planId))
                .ReturnsAsync((string?)null); // Free plan returns null

            var result = await _controller.Subscribe(new CreateSubscriptionRequestDto { PlanId = planId }) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task Subscribe_ShouldReturn200_WithIframeUrl_WhenPaidPlan()
        {
            SetupUser("user-1");
            var planId = Guid.NewGuid();
            _serviceMock.Setup(s => s.CreateSubscriptionAsync("user-1", planId))
                .ReturnsAsync("https://accept.paymob.com/api/acceptance/iframes/123");

            var result = await _controller.Subscribe(new CreateSubscriptionRequestDto { PlanId = planId }) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // Cancel
        // ============================================================

        [Fact]
        public async Task Cancel_ShouldReturn401_WhenNoUser()
        {
            SetupNoUser();

            var result = await _controller.Cancel() as UnauthorizedResult;

            Assert.NotNull(result);
            Assert.Equal(401, result!.StatusCode);
        }

        [Fact]
        public async Task Cancel_ShouldReturn200_WhenSuccess()
        {
            SetupUser("user-1");
            _serviceMock.Setup(s => s.CancelSubscriptionAsync("user-1")).Returns(Task.CompletedTask);

            var result = await _controller.Cancel() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }
    }
}
