using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Settings;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.BLL.Features.Subscription.Services
{
    public class PaymobService : IPaymobService
    {
        private readonly HttpClient _httpClient;
        private readonly PaymobSettings _settings;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public PaymobService(HttpClient httpClient, IOptions<PaymobSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        // =====================================================================
        // Step 1 — Authenticate
        // =====================================================================
        public async Task<string> AuthenticateAsync()
        {
            var body = new { api_key = _settings.ApiKey };
            var response = await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}/auth/tokens", body);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("token").GetString()
                   ?? throw new Exception("Paymob auth returned null token");
        }

        // =====================================================================
        // Step 2 — Create Order
        // =====================================================================
        public async Task<int> CreateOrderAsync(int amountCents, string authToken, string merchantOrderId)
        {
            var body = new
            {
                auth_token = authToken,
                delivery_needed = false,
                amount_cents = amountCents,
                currency = "EGP",
                merchant_order_id = merchantOrderId,
                items = Array.Empty<object>()
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}/ecommerce/orders", body);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("id").GetInt32();
        }

        // =====================================================================
        // Step 3 — Get Payment Key
        // =====================================================================
        public async Task<string> GetPaymentKeyAsync(
            int orderId, int amountCents, string authToken)
        {
            var body = new
            {
                auth_token = authToken,
                amount_cents = amountCents,
                expiration = 3600,
                order_id = orderId,
                billing_data = new
                {
                    apartment = "NA",
                    email = "NA",
                    floor = "NA",
                    first_name = "NA",
                    street = "NA",
                    building = "NA",
                    phone_number = "NA",
                    shipping_method = "NA",
                    postal_code = "NA",
                    city = "NA",
                    country = "NA",
                    last_name = "NA",
                    state = "NA"
                },
                currency = "EGP",
                integration_id = int.Parse(_settings.IntegrationId),
                lock_order_when_paid = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}/acceptance/payment_keys", body);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("token").GetString()
                   ?? throw new Exception("Paymob returned null payment key");
        }

        // =====================================================================
        // Combine: auth → order → payment key → iframe URL
        // =====================================================================
        public async Task<string> InitiatePaymentAsync(
            string userId, Plan plan, Guid subscriptionId, string paymobOrderId)
        {
            var amountCents = (int)(plan.PriceMonthly * 100);

            var authToken = await AuthenticateAsync();

            var orderId = await CreateOrderAsync(amountCents, authToken, paymobOrderId);

            var paymentKey = await GetPaymentKeyAsync(orderId, amountCents, authToken);

            return $"https://accept.paymob.com/api/acceptance/iframes/{_settings.IframeId}?payment_token={paymentKey}";
        }

        // =====================================================================
        // HMAC Verification (SHA-512)
        // =====================================================================
        /// <summary>
        /// Verifies HMAC using Paymob's documented field concatenation order:
        /// amount_cents, created_at, currency, error_occured, has_parent_transaction,
        /// id, integration_id, is_3d_secure, is_auth, is_capture, is_refunded,
        /// is_standalone_payment, is_voided, order.id, owner, pending,
        /// source_data.pan, source_data.sub_type, source_data.type, success
        /// </summary>
        public bool VerifyHmac(Dictionary<string, string> fields, string receivedHmac)
        {
            // Paymob's documented concatenation order
            var orderedKeys = new[]
            {
                "amount_cents", "created_at", "currency", "error_occured",
                "has_parent_transaction", "id", "integration_id", "is_3d_secure",
                "is_auth", "is_capture", "is_refunded", "is_standalone_payment",
                "is_voided", "order", "owner", "pending",
                "source_data.pan", "source_data.sub_type", "source_data.type", "success"
            };

            var concatenated = string.Concat(orderedKeys.Select(k =>
                fields.TryGetValue(k, out var v) ? v : ""));

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_settings.HmacSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
            var computed = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            return string.Equals(computed, receivedHmac, StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        // Refund Payment (Paymob Refund API)
        // =====================================================================
        public async Task<bool> RefundPaymentAsync(string transactionId, decimal amount)
        {
            var authToken = await AuthenticateAsync();
            var amountCents = (int)(amount * 100);

            var body = new
            {
                auth_token = authToken,
                transaction_id = long.Parse(transactionId),
                amount_cents = amountCents
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_settings.BaseUrl}/acceptance/void_refund/refund", body);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception($"Paymob Refund API failed: {content}");
            }

            return true;
        }
    }
}
