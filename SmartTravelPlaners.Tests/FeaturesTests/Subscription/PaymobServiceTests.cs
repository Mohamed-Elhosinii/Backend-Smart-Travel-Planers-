using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

using SmartTravelPlaners.BLL.Features.Subscription.Services;
using SmartTravelPlaners.BLL.Features.Subscription.Settings;
using SmartTravelPlaners.DAL.Entities;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Payment
{
    public class PaymobServiceTests
    {
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly PaymobService _service;

        public PaymobServiceTests()
        {
            _handlerMock = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_handlerMock.Object)
            {
                BaseAddress = new Uri("https://paymob.test")
            };

            var options = Options.Create(new PaymobSettings
            {
                BaseUrl = "https://paymob.test",
                ApiKey = "test-key",
                IntegrationId = "123",
                IframeId = "456",
                HmacSecret = "secret"
            });

            _service = new PaymobService(_httpClient, options);
        }

        // ============================================================
        // Authenticate
        // ============================================================

        [Fact]
        public async Task AuthenticateAsync_ShouldReturnToken()
        {
            SetupResponse(HttpStatusCode.OK, new { token = "abc123" });

            var result = await _service.AuthenticateAsync();

            Assert.Equal("abc123", result);
        }

        // ============================================================
        // Create Order
        // ============================================================

        [Fact]
        public async Task CreateOrderAsync_ShouldReturnOrderId()
        {
            SetupResponse(HttpStatusCode.OK, new { id = 99 });

            var result = await _service.CreateOrderAsync(1000, "token", "order-1");

            Assert.Equal(99, result);
        }

        // ============================================================
        // Get Payment Key
        // ============================================================

        [Fact]
        public async Task GetPaymentKeyAsync_ShouldReturnKey()
        {
            SetupResponse(HttpStatusCode.OK, new { token = "payment-key" });

            var result = await _service.GetPaymentKeyAsync(1, 1000, "token");

            Assert.Equal("payment-key", result);
        }

        // ============================================================
        // Initiate Payment 
        // ============================================================

        [Fact]
        public async Task InitiatePaymentAsync_ShouldReturnIframeUrl()
        {
            SetupMultipleResponses(
                new { token = "auth-token" },   // auth
                new { id = 10 },               // order
                new { token = "pay-key" }      // payment key
            );

            var plan = new Plan { PriceMonthly = 100 };

            var result = await _service.InitiatePaymentAsync(
                "user1",
                plan,
                Guid.NewGuid(),
                "order123");

            Assert.Contains("pay-key", result);
            Assert.Contains("456", result); // iframe id
        }

        // ============================================================
        // Verify HMAC
        // ============================================================

        [Fact]
        public void VerifyHmac_ShouldReturnTrue_WhenValid()
        {
            var fields = new Dictionary<string, string>
            {
                { "amount_cents", "1000" },
                { "created_at", "date" },
                { "currency", "EGP" },
                { "error_occured", "false" },
                { "has_parent_transaction", "false" },
                { "id", "1" },
                { "integration_id", "123" },
                { "is_3d_secure", "false" },
                { "is_auth", "false" },
                { "is_capture", "false" },
                { "is_refunded", "false" },
                { "is_standalone_payment", "true" },
                { "is_voided", "false" },
                { "order", "1" },
                { "owner", "1" },
                { "pending", "false" },
                { "source_data.pan", "****" },
                { "source_data.sub_type", "type" },
                { "source_data.type", "card" },
                { "success", "true" }
            };

            var concatenated = string.Concat(fields.Values);

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes("secret"));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
            var correctHmac = BitConverter.ToString(hash).Replace("-", "").ToLower();

            var result = _service.VerifyHmac(fields, correctHmac);

            Assert.True(result);
        }

        // ============================================================
        // Refund
        // ============================================================

        [Fact]
        public async Task RefundPaymentAsync_ShouldReturnTrue_WhenSuccess()
        {
            SetupMultipleResponses(
                new { token = "auth-token" }, // auth
                new { success = true }        // refund
            );

            var result = await _service.RefundPaymentAsync("123", 100);

            Assert.True(result);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void SetupResponse(HttpStatusCode status, object content)
        {
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(CreateResponse(status, content));
        }

        private void SetupMultipleResponses(params object[] responses)
        {
            var sequence = _handlerMock
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

            foreach (var r in responses)
            {
                sequence = sequence.ReturnsAsync(CreateResponse(HttpStatusCode.OK, r));
            }
        }

        private HttpResponseMessage CreateResponse(HttpStatusCode status, object content)
        {
            return new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(
                    JsonSerializer.Serialize(content),
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}