using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using System.Text;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class PaymentsControllerTests
    {
        private readonly Mock<ISubscriptionService> _subscriptionMock;
        private readonly Mock<IPaymobService> _paymobMock;
        private readonly Mock<ILogger<PaymentsController>> _loggerMock;
        private readonly PaymentsController _controller;

        public PaymentsControllerTests()
        {
            _subscriptionMock = new Mock<ISubscriptionService>();
            _paymobMock = new Mock<IPaymobService>();
            _loggerMock = new Mock<ILogger<PaymentsController>>();

            _controller = new PaymentsController(
                _subscriptionMock.Object,
                _paymobMock.Object,
                _loggerMock.Object);
        }

        private void SetupRequestBody(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var stream = new MemoryStream(bytes);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request = { Body = stream }
                }
            };
        }

        // ============================================================
        // Callback
        // ============================================================

        [Fact]
        public void Callback_ShouldReturn200()
        {
            var result = _controller.Callback() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // Webhook — Empty Body
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldReturn200_WhenBodyEmpty()
        {
            SetupRequestBody("");

            var result = await _controller.Webhook() as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // Webhook — Invalid JSON
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldReturn200_WhenPayloadNull()
        {
            SetupRequestBody("{}");

            var result = await _controller.Webhook() as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // Webhook — Invalid HMAC
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldReturn200_WhenHmacInvalid()
        {
            var json = """
            {
                "hmac": "invalidhmac",
                "obj": {
                    "id": 1,
                    "success": true,
                    "amount_cents": 10000,
                    "created_at": "2026-01-01T00:00:00",
                    "currency": "EGP",
                    "error_occured": false,
                    "has_parent_transaction": false,
                    "integration_id": 1,
                    "is_3d_secure": false,
                    "is_auth": false,
                    "is_capture": false,
                    "is_refunded": false,
                    "is_standalone_payment": true,
                    "is_voided": false,
                    "pending": false,
                    "merchant_order_id": "order-123",
                    "order": { "id": 99 },
                    "source_data": { "pan": "1234", "sub_type": "card", "type": "card" }
                }
            }
            """;

            SetupRequestBody(json);
            _paymobMock.Setup(p => p.VerifyHmac(It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .Returns(false);

            var result = await _controller.Webhook() as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            _subscriptionMock.Verify(s => s.ActivateSubscriptionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ============================================================
        // Webhook — Valid HMAC + Success → Activate
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldActivateSubscription_WhenHmacValidAndSuccess()
        {
            var json = """
            {
                "hmac": "validhmac",
                "obj": {
                    "id": 1,
                    "success": true,
                    "amount_cents": 10000,
                    "created_at": "2026-01-01T00:00:00",
                    "currency": "EGP",
                    "error_occured": false,
                    "has_parent_transaction": false,
                    "integration_id": 1,
                    "is_3d_secure": false,
                    "is_auth": false,
                    "is_capture": false,
                    "is_refunded": false,
                    "is_standalone_payment": true,
                    "is_voided": false,
                    "pending": false,
                    "merchant_order_id": "order-123",
                    "order": { "id": 99 },
                    "source_data": { "pan": "1234", "sub_type": "card", "type": "card" }
                }
            }
            """;

            SetupRequestBody(json);
            _paymobMock.Setup(p => p.VerifyHmac(It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .Returns(true);
            _subscriptionMock.Setup(s => s.ActivateSubscriptionAsync("order-123", "1"))
                .Returns(Task.CompletedTask);

            var result = await _controller.Webhook() as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            _subscriptionMock.Verify(s => s.ActivateSubscriptionAsync("order-123", "1"), Times.Once);
        }

        // ============================================================
        // Webhook — Valid HMAC + Not Success → Don't Activate
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldNotActivate_WhenTransactionNotSuccess()
        {
            var json = """
            {
                "hmac": "validhmac",
                "obj": {
                    "id": 2,
                    "success": false,
                    "amount_cents": 10000,
                    "created_at": "2026-01-01T00:00:00",
                    "currency": "EGP",
                    "error_occured": true,
                    "has_parent_transaction": false,
                    "integration_id": 1,
                    "is_3d_secure": false,
                    "is_auth": false,
                    "is_capture": false,
                    "is_refunded": false,
                    "is_standalone_payment": true,
                    "is_voided": false,
                    "pending": false,
                    "merchant_order_id": "order-456",
                    "order": { "id": 100 },
                    "source_data": { "pan": "1234", "sub_type": "card", "type": "card" }
                }
            }
            """;

            SetupRequestBody(json);
            _paymobMock.Setup(p => p.VerifyHmac(It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .Returns(true);

            var result = await _controller.Webhook() as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            _subscriptionMock.Verify(s => s.ActivateSubscriptionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}