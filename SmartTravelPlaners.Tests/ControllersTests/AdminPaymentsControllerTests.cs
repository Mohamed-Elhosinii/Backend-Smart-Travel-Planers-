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
    public class AdminPaymentsControllerTests
    {
        private readonly Mock<IAdminDashboardService> _serviceMock;
        private readonly AdminPaymentsController _controller;

        public AdminPaymentsControllerTests()
        {
            _serviceMock = new Mock<IAdminDashboardService>();
            _controller = new AdminPaymentsController(_serviceMock.Object);
        }

        private (IEnumerable<AdminPaymentTransactionDto> transactions, int totalCount) MakeResult()
        {
            var transactions = new List<AdminPaymentTransactionDto>
    {
        new AdminPaymentTransactionDto
        {
            Id = Guid.NewGuid(),
            UserEmail = "user1@test.com",
            PlanName = "Basic Plan",
            Amount = 100,
            Status = "Success",
            CreatedAt = DateTime.UtcNow,
            PaymobOrderId = "ORD-1001",
            PaymobTransactionId = "TXN-2001"
        },
        new AdminPaymentTransactionDto
        {
            Id = Guid.NewGuid(),
            UserEmail = "user2@test.com",
            PlanName = "Pro Plan",
            Amount = 200,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            PaymobOrderId = "ORD-1002",
            PaymobTransactionId = null
        }
    };

            return (transactions, transactions.Count);
        }

        // ============================================================
        // GetPayments — Success (default pagination)
        // ============================================================

        [Fact]
        public async Task GetPayments_ShouldReturn200_WhenSuccess_DefaultPagination()
        {
            var mockResult = MakeResult();

            _serviceMock
                .Setup(s => s.GetPaymentsHistoryAsync(1, 10))
                .ReturnsAsync(mockResult);

            var result = await _controller.GetPayments() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // GetPayments — Success (custom pagination)
        // ============================================================

        [Fact]
        public async Task GetPayments_ShouldReturn200_WhenSuccess_CustomPagination()
        {
            var mockResult = MakeResult();

            _serviceMock
                .Setup(s => s.GetPaymentsHistoryAsync(2, 5))
                .ReturnsAsync(mockResult);

            var result = await _controller.GetPayments(2, 5) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // GetPayments — Service throws exception
        // ============================================================

        [Fact]
        public async Task GetPayments_ShouldReturn400_WhenServiceThrows()
        {
            _serviceMock
                .Setup(s => s.GetPaymentsHistoryAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Something went wrong"));

            var result = await _controller.GetPayments() as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }
    }
}