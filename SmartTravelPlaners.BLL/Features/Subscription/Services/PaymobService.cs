using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<PaymobService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public PaymobService(HttpClient httpClient, IOptions<PaymobSettings> settings, ILogger<PaymobService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        // =====================================================================
        // Step 1 — Authenticate
        // =====================================================================
        public async Task<string> AuthenticateAsync()
        {
            try
            {
                _logger.LogInformation("Paymob authentication initiated");
                var body = new { api_key = _settings.ApiKey };
                var response = await _httpClient.PostAsJsonAsync(
                    $"{_settings.BaseUrl}/auth/tokens", body);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var token = doc.RootElement.GetProperty("token").GetString()
                       ?? throw new Exception("Paymob auth returned null token");

                _logger.LogInformation("Paymob authentication successful");
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paymob authentication failed. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Step 2 — Create Order
        // =====================================================================
        public async Task<int> CreateOrderAsync(int amountCents, string authToken, string merchantOrderId)
        {
            try
            {
                _logger.LogInformation("Creating Paymob order. MerchantOrderId: {MerchantOrderId}, Amount: {Amount} cents", merchantOrderId, amountCents);

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
                var orderId = doc.RootElement.GetProperty("id").GetInt32();

                _logger.LogInformation("Paymob order created successfully. OrderId: {OrderId}, MerchantOrderId: {MerchantOrderId}", orderId, merchantOrderId);
                return orderId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paymob order creation failed for MerchantOrderId: {MerchantOrderId}. Error: {ErrorMessage}", merchantOrderId, ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Step 3 — Get Payment Key
        // =====================================================================
        public async Task<string> GetPaymentKeyAsync(
            int orderId, int amountCents, string authToken, UserProfile userProfile)
        {
            try
            {
                _logger.LogInformation("Requesting payment key from Paymob. OrderId: {OrderId}, Amount: {Amount} cents", orderId, amountCents);

                var firstName = "NA";
                var lastName = "NA";
                var email = "NA";
                var phone = "NA";

                if (userProfile.AspNetUser != null)
                {
                    var names = userProfile.AspNetUser.FullName?.Split(' ', 2);
                    firstName = names != null && names.Length > 0 ? names[0] : "NA";
                    lastName = names != null && names.Length > 1 ? names[1] : "NA";
                    email = userProfile.AspNetUser.Email ?? "NA";
                    phone = userProfile.AspNetUser.PhoneNumber ?? "NA";
                }

                var body = new
                {
                    auth_token = authToken,
                    amount_cents = amountCents,
                    expiration = 3600,
                    order_id = orderId,
                    billing_data = new
                    {
                        apartment = "NA",
                        email = email,
                        floor = "NA",
                        first_name = firstName,
                        street = "NA",
                        building = "NA",
                        phone_number = phone,
                        shipping_method = "NA",
                        postal_code = "NA",
                        city = "NA",
                        country = "NA",
                        last_name = lastName,
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
                var paymentKey = doc.RootElement.GetProperty("token").GetString()
                       ?? throw new Exception("Paymob returned null payment key");

                _logger.LogInformation("Payment key received successfully from Paymob for OrderId: {OrderId}", orderId);
                return paymentKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payment key from Paymob for OrderId: {OrderId}. Error: {ErrorMessage}", orderId, ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Combine: auth → order → payment key → iframe URL
        // =====================================================================
        public async Task<string> InitiatePaymentAsync(
            UserProfile userProfile, Plan plan, Guid subscriptionId, string paymobOrderId)
        {
            try
            {
                _logger.LogInformation("Initiating Paymob payment for SubscriptionId: {SubscriptionId}, UserId: {UserId}, Plan: {PlanName}", subscriptionId, userProfile.AspNetUserId, plan.Name);

                var amountCents = (int)(plan.PriceMonthly * 100);

                var authToken = await AuthenticateAsync();

                var orderId = await CreateOrderAsync(amountCents, authToken, paymobOrderId);

                var paymentKey = await GetPaymentKeyAsync(orderId, amountCents, authToken, userProfile);

                var iframeUrl = $"https://accept.paymob.com/api/acceptance/iframes/{_settings.IframeId}?payment_token={paymentKey}";
                _logger.LogInformation("Payment initiated successfully for SubscriptionId: {SubscriptionId}", subscriptionId);

                return iframeUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment initiation failed for SubscriptionId: {SubscriptionId}, UserId: {UserId}. Error: {ErrorMessage}", subscriptionId, userProfile.AspNetUserId, ex.Message);
                throw;
            }
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
            try
            {
                _logger.LogInformation("Verifying Paymob HMAC signature");

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

                var isValid = string.Equals(computed, receivedHmac, StringComparison.OrdinalIgnoreCase);

                if (isValid)
                {
                    _logger.LogInformation("HMAC signature verified successfully");
                }
                else
                {
                    _logger.LogWarning("HMAC signature verification failed - signature mismatch");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HMAC verification failed. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Refund Payment (Paymob Refund API)
        // =====================================================================
        public async Task<bool> RefundPaymentAsync(string transactionId, decimal amount)
        {
            try
            {
                _logger.LogInformation("Refund initiated for TransactionId: {TransactionId}, Amount: {Amount}", transactionId, amount);

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
                    _logger.LogError("Paymob refund API failed for TransactionId: {TransactionId}. Response: {Response}", transactionId, content);
                    throw new Exception($"Paymob Refund API failed: {content}");
                }

                _logger.LogInformation("Refund processed successfully for TransactionId: {TransactionId}, Amount: {Amount}", transactionId, amount);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refund failed for TransactionId: {TransactionId}. Error: {ErrorMessage}", transactionId, ex.Message);
                throw;
            }
        }
    }
}
