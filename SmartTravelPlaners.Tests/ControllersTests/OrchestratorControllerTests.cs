using Castle.Core.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using SmartTravelPlaners.PL.Controllers;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class OrchestratorTestControllerTests
    {
        private readonly Mock<ITripOrchestratorService> _orchestratorMock;
        private readonly Mock<IUnitOfWork> _uowMock;
      private readonly Mock<ILogger<OrchestratorTestController>> _loggerMock;
        private readonly OrchestratorTestController _controller;

        public OrchestratorTestControllerTests()
        {
            _orchestratorMock = new Mock<ITripOrchestratorService>();
            _uowMock = new Mock<IUnitOfWork>();
            _loggerMock = new Mock<ILogger<OrchestratorTestController>>();
            _controller = new OrchestratorTestController(_orchestratorMock.Object, _uowMock.Object, _loggerMock.Object);
        }

        // ============================================================
        // Build
        // ============================================================

        [Fact]
        public async Task Build_ShouldReturn200_WhenSuccess()
        {
            var tripId = Guid.NewGuid();
            _orchestratorMock.Setup(o => o.BuildTripPlanAsync(tripId))
                .ReturnsAsync(new TripPlanDto
                {
                    TripId = tripId,
                    Destination = "Paris"
                });

            var result = await _controller.Build(tripId) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task Build_ShouldReturn400_WhenExceptionThrown()
        {
            var tripId = Guid.NewGuid();
            _orchestratorMock.Setup(o => o.BuildTripPlanAsync(tripId))
                .ThrowsAsync(new Exception("Trip not found"));

            var result = await _controller.Build(tripId) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // SeedTrip
        // ============================================================

        [Fact]
        public async Task SeedTrip_ShouldReturn200_WithTripId()
        {
            var tripRepoMock = new Mock<ITripRepository>();
            tripRepoMock.Setup(r => r.AddAsync(It.IsAny<DAL.Entities.Trip>())).Returns(Task.CompletedTask);
            _uowMock.Setup(u => u.Trips).Returns(tripRepoMock.Object);
            _uowMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            var result = await _controller.SeedTrip() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }
    }
}
