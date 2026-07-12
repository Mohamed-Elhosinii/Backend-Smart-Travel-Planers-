using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly PaymentsController _controller;

        public PaymentsControllerTests()
        {
            _subscriptionMock = new Mock<ISubscriptionService>();
            _paymobMock = new Mock<IPaymobService>();
            _loggerMock = new Mock<ILogger<PaymentsController>>();
            _configurationMock = new Mock<IConfiguration>();

            _controller = new PaymentsController(
                _subscriptionMock.Object,
                _paymobMock.Object,
                _loggerMock.Object,
                _configurationMock.Object
            );
        }

        private void SetupRequestBody(string json, string queryString = "")
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var stream = new MemoryStream(bytes);
            var httpContext = new DefaultHttpContext
            {
                Request = { Body = stream }
            };
            if (!string.IsNullOrEmpty(queryString))
            {
                httpContext.Request.QueryString = new QueryString(queryString);
            }

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        // ============================================================
        // Callback
        // ============================================================

        [Fact]
        public void Callback_ShouldRedirectToFrontendUrl_WithQueryString()
        {
            _configurationMock.Setup(c => c["FrontendUrl"]).Returns("https://frontend-smart-travel-planers.vercel.app");

            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?id=123&success=true");
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = _controller.Callback() as RedirectResult;

            Assert.NotNull(result);
            Assert.Equal("https://frontend-smart-travel-planers.vercel.app/payment-status?id=123&success=true", result!.Url);
        }

        [Fact]
        public void Callback_ShouldUseDefaultFrontendUrl_WhenConfigurationMissing()
        {
            _configurationMock.Setup(c => c["FrontendUrl"]).Returns((string?)null);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("");
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = _controller.Callback() as RedirectResult;

            Assert.NotNull(result);
            Assert.StartsWith("https://frontend-smart-travel-planers.vercel.app/payment-status", result!.Url);
        }

        // ============================================================
        // Webhook — Empty Body
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldReturn200_WhenBodyEmpty()
        {
            SetupRequestBody("");

            var result = await _controller.Webhook(hmac: "") as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // Webhook — Invalid JSON / Missing Obj
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldReturn200_WhenPayloadNull()
        {
            SetupRequestBody("{}");

            var result = await _controller.Webhook(hmac: "") as OkResult;

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

            var result = await _controller.Webhook(hmac: "") as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            _subscriptionMock.Verify(s => s.ActivateSubscriptionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

            var result = await _controller.Webhook(hmac: "") as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            _subscriptionMock.Verify(s => s.ActivateSubscriptionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ============================================================
        // Webhook — Missing MerchantOrderId → Don't Activate
        // ============================================================

        [Fact]
        public async Task Webhook_ShouldNotActivate_WhenMerchantOrderIdMissing()
        {
            var json = """
            {
                "hmac": "validhmac",
                "obj": {
                    "id": 3,
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
                    "order": { "id": 102 },
                    "source_data": { "pan": "1234", "sub_type": "card", "type": "card" }
                }
            }
            """;

            SetupRequestBody(json);
            _paymobMock.Setup(p => p.VerifyHmac(It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .Returns(true);

            var result = await _controller.Webhook(hmac: "") as OkResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
            _subscriptionMock.Verify(s => s.ActivateSubscriptionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}