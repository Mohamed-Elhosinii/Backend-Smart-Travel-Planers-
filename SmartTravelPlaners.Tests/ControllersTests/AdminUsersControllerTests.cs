using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Admin.DTOs;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class AdminUsersControllerTests
    {
        private readonly Mock<IAdminDashboardService> _serviceMock;
        private readonly AdminUsersController _controller;

        public AdminUsersControllerTests()
        {
            _serviceMock = new Mock<IAdminDashboardService>();
            _controller = new AdminUsersController(_serviceMock.Object);
        }

        // ============================================================
        // GET USERS
        // ============================================================

        [Fact]
        public async Task GetUsers_ShouldReturn200_WhenSuccess()
        {
            var mockUsers = new List<AdminUserListItemDto>
            {
                new AdminUserListItemDto { UserId = "1", Email = "a@test.com" },
                new AdminUserListItemDto { UserId = "2", Email = "b@test.com" }
            };

            _serviceMock
                .Setup(s => s.GetUsersListAsync(null, 1, 10))
                .ReturnsAsync((mockUsers, mockUsers.Count));

            var result = await _controller.GetUsers(null) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // GET USERS with search
        // ============================================================

        [Fact]
        public async Task GetUsers_ShouldReturn200_WhenSearchProvided()
        {
            var mockUsers = new List<AdminUserListItemDto>
            {
                new AdminUserListItemDto { UserId = "1", Email = "search@test.com" }
            };

            _serviceMock
                .Setup(s => s.GetUsersListAsync("search", 1, 10))
                .ReturnsAsync((mockUsers, mockUsers.Count));

            var result = await _controller.GetUsers("search") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // FORCE UPDATE PLAN
        // ============================================================

        [Fact]
        public async Task ForceUpdatePlan_ShouldReturn200_WhenSuccess()
        {
            var userId = "user1";
            var planId = Guid.NewGuid();

            _serviceMock
                .Setup(s => s.UpdateUserSubscriptionPlanAsync(userId, planId))
                .Returns(Task.CompletedTask);

            var result = await _controller.ForceUpdatePlan(userId, planId) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // TOGGLE USER STATUS
        // ============================================================

        [Fact]
        public async Task ToggleUserStatus_ShouldReturn200_WhenSuccess()
        {
            var userId = "user1";

            _serviceMock
                .Setup(s => s.ToggleUserStatusAsync(userId))
                .Returns(Task.CompletedTask);

            var result = await _controller.ToggleUserStatus(userId) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // EXCEPTION CASE (GET USERS)
        // ============================================================

        [Fact]
        public async Task GetUsers_ShouldReturn400_WhenExceptionThrown()
        {
            _serviceMock
                .Setup(s => s.GetUsersListAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Error"));

            var result = await _controller.GetUsers(null) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }
    }
}
