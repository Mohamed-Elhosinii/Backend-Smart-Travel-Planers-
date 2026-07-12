using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.DTOs;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Services;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using SmartTravelPlaners.PL.Controllers;
using System.Security.Claims;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class TripControllerTests
    {
        private readonly Mock<ITripCreationService> _serviceMock;
        private readonly Mock<ITripRepository> _tripRepoMock;
        private readonly Mock<IPlacesApiService> _placesServiceMock;
        private readonly Mock<ITripOrchestratorService> _orchestratorMock;
        private readonly Mock<ILogger<TripController>> _loggerMock;
        private readonly Mock<IUserProfileRepository> _userProfileRepoMock;
        private readonly TripController _controller;

        public TripControllerTests()
        {
            _serviceMock = new Mock<ITripCreationService>();
            _tripRepoMock = new Mock<ITripRepository>();
            _placesServiceMock = new Mock<IPlacesApiService>();
            _orchestratorMock = new Mock<ITripOrchestratorService>();
            _loggerMock = new Mock<ILogger<TripController>>();
            _userProfileRepoMock = new Mock<IUserProfileRepository>();

            _controller = new TripController(
                _serviceMock.Object,
                _tripRepoMock.Object,
                _placesServiceMock.Object,
                _orchestratorMock.Object,
                _loggerMock.Object);
        }

        private void SetupUser(string userId = "user-1")
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "Test");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private void SetupAnonymousUser()
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
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
        // QuickPlan
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

        [Fact]
        public async Task QuickPlan_ShouldReturn401_WhenUserIdMissing()
        {
            SetupAnonymousUser();

            var result = await _controller.QuickPlan(MakeDto());

            Assert.IsType<UnauthorizedResult>(result);
        }

        // ============================================================
        // GetTrips
        // ============================================================

        [Fact]
        public async Task GetTrips_ShouldReturn401_WhenUserIdMissing()
        {
            SetupAnonymousUser();

            var result = await _controller.GetTrips(_userProfileRepoMock.Object);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task GetTrips_ShouldReturn401_WhenProfileNotFound()
        {
            SetupUser("user-1");
            _userProfileRepoMock
                .Setup(r => r.GetUserProfileWithPreferencesAsync("user-1"))
                .ReturnsAsync((UserProfile?)null);

            var result = await _controller.GetTrips(_userProfileRepoMock.Object);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task GetTrips_ShouldReturn200WithSummaries_WhenSuccess()
        {
            SetupUser("user-1");
            var profileId = Guid.NewGuid();
            _userProfileRepoMock
                .Setup(r => r.GetUserProfileWithPreferencesAsync("user-1"))
                .ReturnsAsync(new UserProfile { Id = profileId });

            var trip = new Trip
            {
                Id = Guid.NewGuid(),
                Destination = "Paris",
                OriginCity = "Cairo",
                StartDate = DateOnly.Parse("2025-06-01"),
                EndDate = DateOnly.Parse("2025-06-07"),
                BudgetTotal = 3000,
                BudgetSpent = 0,
                Preferences = new List<TripPreference>()
            };

            _tripRepoMock
                .Setup(r => r.GetUserTripsAsync(profileId))
                .ReturnsAsync(new List<Trip> { trip });

            var result = await _controller.GetTrips(_userProfileRepoMock.Object) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            var summaries = result.Value as List<TripSummaryDto>;
            Assert.NotNull(summaries);
            Assert.Single(summaries!);
            Assert.Equal("Paris", summaries![0].Destination);
        }

        // ============================================================
        // GetSuggestions
        // ============================================================

        [Fact]
        public async Task GetSuggestions_ShouldReturn404_WhenTripNotFound()
        {
            var tripId = Guid.NewGuid();
            _tripRepoMock
                .Setup(r => r.GetByIdAsync(tripId))
                .ReturnsAsync((Trip?)null);

            var result = await _controller.GetSuggestions(tripId);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetSuggestions_ShouldReturn200_WhenSuccess()
        {
            var tripId = Guid.NewGuid();
            var trip = new Trip { Id = tripId, Destination = "Paris" };

            _tripRepoMock
                .Setup(r => r.GetByIdAsync(tripId))
                .ReturnsAsync(trip);

            // NOTE: adjust return type below to match your real IPlacesApiService.SearchAsync signature
            _placesServiceMock
    .Setup(p => p.SearchAsync("Paris", null, 6))
    .ReturnsAsync(new List<PlaceDto>());

            var result = await _controller.GetSuggestions(tripId) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // UpdateActivityImage
        // ============================================================

        [Fact]
        public async Task UpdateActivityImage_ShouldReturn400_WhenImageUrlMissing()
        {
            var activityId = Guid.NewGuid();
            var dto = new UpdateActivityImageDto { ImageUrl = "" };

            var result = await _controller.UpdateActivityImage(activityId, dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateActivityImage_ShouldReturn200_WhenSuccess()
        {
            var activityId = Guid.NewGuid();
            var dto = new UpdateActivityImageDto { ImageUrl = "https://example.com/img.jpg" };

            _orchestratorMock
                .Setup(o => o.UpdateActivityImageAsync(activityId, dto.ImageUrl))
                .Returns(Task.CompletedTask);

            var result = await _controller.UpdateActivityImage(activityId, dto);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task UpdateActivityImage_ShouldReturn500_WhenThrows()
        {
            var activityId = Guid.NewGuid();
            var dto = new UpdateActivityImageDto { ImageUrl = "https://example.com/img.jpg" };

            _orchestratorMock
                .Setup(o => o.UpdateActivityImageAsync(activityId, dto.ImageUrl))
                .ThrowsAsync(new Exception("fail"));

            var result = await _controller.UpdateActivityImage(activityId, dto) as ObjectResult;

            Assert.NotNull(result);
            Assert.Equal(500, result!.StatusCode);
        }
    }
}