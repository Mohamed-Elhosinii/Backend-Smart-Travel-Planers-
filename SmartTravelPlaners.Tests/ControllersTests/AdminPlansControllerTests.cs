using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;
using SmartTravelPlaners.PL.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class AdminPlansControllerTests
    {
        private readonly Mock<IAdminDashboardService> _serviceMock;
        private readonly AdminPlansController _controller;

        public AdminPlansControllerTests()
        {
            _serviceMock = new Mock<IAdminDashboardService>();
            _controller = new AdminPlansController(_serviceMock.Object);
        }

        // ============================================================
        // GET ALL
        // ============================================================

        [Fact]
        public async Task GetAll_ShouldReturn200_WhenSuccess()
        {
            var mockPlans = new List<PlanDto>
            {
                new PlanDto { Id = Guid.NewGuid(), Name = "Basic" },
                new PlanDto { Id = Guid.NewGuid(), Name = "Pro" }
            };

            _serviceMock
                .Setup(s => s.GetAllPlansAsync())
                .ReturnsAsync(mockPlans);

            var result = await _controller.GetAll() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // CREATE
        // ============================================================

        [Fact]
        public async Task Create_ShouldReturn201_WhenSuccess()
        {
            var planDto = new PlanDto
            {
                Id = Guid.NewGuid(),
                Name = "Basic Plan"
            };

            _serviceMock
                .Setup(s => s.CreatePlanAsync(planDto))
                .ReturnsAsync(planDto);

            var result = await _controller.Create(planDto) as CreatedAtActionResult;

            Assert.NotNull(result);
            Assert.Equal(201, result!.StatusCode);
            Assert.Equal("Basic Plan", ((PlanDto)result.Value!).Name);
        }

        // ============================================================
        // UPDATE
        // ============================================================

        [Fact]
        public async Task Update_ShouldReturn200_WhenSuccess()
        {
            var id = Guid.NewGuid();
            var planDto = new PlanDto { Id = id, Name = "Updated Plan" };

            _serviceMock
                .Setup(s => s.UpdatePlanAsync(id, planDto))
                .Returns(Task.CompletedTask);

            var result = await _controller.Update(id, planDto) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // DELETE
        // ============================================================

        [Fact]
        public async Task Delete_ShouldReturn200_WhenSuccess()
        {
            var id = Guid.NewGuid();

            _serviceMock
                .Setup(s => s.DeletePlanAsync(id))
                .Returns(Task.CompletedTask);

            var result = await _controller.Delete(id) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // EXCEPTION CASE (GET ALL)
        // ============================================================

        [Fact]
        public async Task GetAll_ShouldReturn400_WhenExceptionThrown()
        {
            _serviceMock
                .Setup(s => s.GetAllPlansAsync())
                .ThrowsAsync(new Exception("Error"));

            var result = await _controller.GetAll() as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }
    }
}