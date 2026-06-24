using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Trips.DTOs;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Services;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.PL.Controllers;
using System.Security.Claims;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class TripControllerTests
    {
        private readonly Mock<ITripCreationService> _serviceMock;
        private readonly TripController _controller;

        public TripControllerTests()
        {
            _serviceMock = new Mock<ITripCreationService>();
            _controller = new TripController(_serviceMock.Object);
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

        private TripCreateDto MakeDto() => new TripCreateDto
        {
            Destination = "Paris",
            OriginCity = "Cairo",
            StartDate = "2025-06-01",
            EndDate = "2025-06-07",
            NumTravelers = 2,
            BudgetTotal = 3000,
            Preferences = new List<string> { "Culture" }
        };

        // ============================================================
        // QuickPlan - Success
        // ============================================================

        [Fact]
        public async Task QuickPlan_ShouldReturn200_WhenSuccess()
        {
            SetupUser("user-1");
            var tripId = Guid.NewGuid();
            _serviceMock
                .Setup(s => s.CreateAndBuildAsync(It.IsAny<TripCreateDto>(), "user-1"))
                .ReturnsAsync(new TripCreationResult { TripId = tripId, Trip = new Trip { Id = tripId } });

            var result = await _controller.QuickPlan(MakeDto()) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task QuickPlan_ShouldReturnTripId_WhenSuccess()
        {
            SetupUser("user-1");
            var tripId = Guid.NewGuid();
            _serviceMock
                .Setup(s => s.CreateAndBuildAsync(It.IsAny<TripCreateDto>(), "user-1"))
                .ReturnsAsync(new TripCreationResult { TripId = tripId, Trip = new Trip { Id = tripId } });

            var result = await _controller.QuickPlan(MakeDto()) as OkObjectResult;
            var value = result!.Value;
            var prop = value!.GetType().GetProperty("tripId");

            Assert.NotNull(prop);
            Assert.Equal(tripId, prop!.GetValue(value));
        }

        // ============================================================
        // QuickPlan - Limit Reached
        // ============================================================

        [Fact]
        public async Task QuickPlan_ShouldReturn200WithMessage_WhenLimitReached()
        {
            SetupUser("user-1");
            _serviceMock
                .Setup(s => s.CreateAndBuildAsync(It.IsAny<TripCreateDto>(), "user-1"))
                .ReturnsAsync(new TripCreationResult
                {
                    LimitReached = true,
                    Message = TripCreationService.LimitReachedMessage
                });

            var result = await _controller.QuickPlan(MakeDto()) as OkObjectResult;
            var value = result!.Value;
            var prop = value!.GetType().GetProperty("message");

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            Assert.NotNull(prop);
            Assert.Equal(TripCreationService.LimitReachedMessage, prop!.GetValue(value));
        }

        // ============================================================
        // QuickPlan - Exception
        // ============================================================

        [Fact]
        public async Task QuickPlan_ShouldReturn400_WhenThrows()
        {
            SetupUser("user-1");
            _serviceMock
                .Setup(s => s.CreateAndBuildAsync(It.IsAny<TripCreateDto>(), "user-1"))
                .ThrowsAsync(new Exception("User profile not found"));

            var result = await _controller.QuickPlan(MakeDto()) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // QuickPlan - Unauthorized
        // ============================================================

        [Fact]
        public async Task QuickPlan_ShouldReturn401_WhenUserIdMissing()
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            var result = await _controller.QuickPlan(MakeDto());

            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}