using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Admin.DTOs;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using System;
using System.Threading.Tasks;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class AdminStatsControllerTests
    {
        private readonly Mock<IAdminDashboardService> _serviceMock;
        private readonly AdminStatsController _controller;

        public AdminStatsControllerTests()
        {
            _serviceMock = new Mock<IAdminDashboardService>();
            _controller = new AdminStatsController(_serviceMock.Object);
        }

        private AdminStatsDto MakeStats()
        {
            return new AdminStatsDto
            {
                // املئيه حسب الـ DTO عندك
            };
        }

        // ============================================================
        // SUCCESS CASE
        // ============================================================

        [Fact]
        public async Task GetOverview_ShouldReturn200_WhenSuccess()
        {
            var mockStats = MakeStats();

            _serviceMock
                .Setup(s => s.GetOverviewStatsAsync())
                .ReturnsAsync(mockStats);

            var result = await _controller.GetOverview() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // EXCEPTION CASE
        // ============================================================

        [Fact]
        public async Task GetOverview_ShouldReturn400_WhenExceptionThrown()
        {
            _serviceMock
                .Setup(s => s.GetOverviewStatsAsync())
                .ThrowsAsync(new Exception("Something went wrong"));

            var result = await _controller.GetOverview() as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }
    }
}
