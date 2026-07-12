

using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using System.Text.Json;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IPaymobService _paymobService;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IConfiguration _configuration;

        public PaymentsController(
            ISubscriptionService subscriptionService,
            IPaymobService paymobService,
            ILogger<PaymentsController> logger,
            IConfiguration configuration)
        {
            _subscriptionService = subscriptionService;
            _paymobService = paymobService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Paymob webhook endpoint — NO [Authorize].
        /// Paymob calls this server-to-server after a payment is processed.
        /// HMAC is verified BEFORE trusting any field in the payload.
        /// Always returns 200 OK per Paymob's requirements.
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromQuery] string hmac)
        {
            try
            {
                // Read raw body
                using var reader = new StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();

                _logger.LogInformation("Paymob webhook received. Body length: {Length}", rawBody.Length);

                if (string.IsNullOrEmpty(rawBody))
                {
                    _logger.LogWarning("Paymob webhook: request body is empty.");
                    return Ok();
                }

                var payload = JsonSerializer.Deserialize<PaymobWebhookRootDto>(rawBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (payload?.Obj == null)
                {
                    _logger.LogWarning("Paymob webhook: payload or obj is null.");
                    return Ok();
                }

                // ============================================================
                // CRITICAL: Verify HMAC BEFORE trusting any payload fields
                // ============================================================
                var hmacFields = BuildHmacFields(payload.Obj);
                
                // Paymob sends HMAC in query string, but we also check payload just in case
                var actualHmac = !string.IsNullOrEmpty(hmac) ? hmac : payload.Hmac;
                    
                var isValid = _paymobService.VerifyHmac(hmacFields, actualHmac);

                if (!isValid)
                {
                    _logger.LogWarning(
                        "⚠️ SECURITY: Invalid HMAC on Paymob webhook. OrderId={OrderId}, TransactionId={TransactionId}. " +
                        "Possible tampering — NOT activating subscription.",
                        payload.Obj.Order?.Id, payload.Obj.Id);
                    return Ok(); // Return 200 regardless per Paymob's requirements
                }

                // HMAC valid — proceed
                if (!payload.Obj.Success)
                {
                    _logger.LogInformation(
                        "Paymob webhook: Transaction {TransactionId} was NOT successful. Skipping activation.",
                        payload.Obj.Id);
                    return Ok();
                }

                var merchantOrderId = payload.Obj.Order?.MerchantOrderId ?? payload.Obj.MerchantOrderId;
                var transactionId = payload.Obj.Id.ToString();

                if (string.IsNullOrEmpty(merchantOrderId))
                {
                    _logger.LogWarning(
                        "Paymob webhook: merchant_order_id missing from successful transaction {TransactionId}. " +
                        "Cannot match to a local order.",
                        transactionId);
                    return Ok();
                }

                try
                {
                    // Match using OUR merchant_order_id (generated at order-creation time)
                    await _subscriptionService.ActivateSubscriptionAsync(merchantOrderId, transactionId);

                    _logger.LogInformation(
                        "Subscription activated via webhook. MerchantOrderId={MerchantOrderId}, TransactionId={TransactionId}",
                        merchantOrderId, transactionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to activate subscription. MerchantOrderId={MerchantOrderId}",
                        merchantOrderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paymob webhook processing failed.");
            }

            // Always return 200 OK per Paymob's requirements
            return Ok();
        }

        /// <summary>
        /// Browser callback URL — public, just returns a simple response.
        /// Does NOT trust query params to activate anything.
        /// The actual activation happens via the webhook endpoint above.
        /// </summary>
        [HttpGet("callback")]
        public IActionResult Callback()
        {
            var queryString = Request.QueryString.Value;
            var frontendUrl = _configuration["FrontendUrl"] ?? "https://frontend-smart-travel-planers.vercel.app";
            return Redirect($"{frontendUrl}/payment-status" + queryString);
        }

        /// <summary>
        /// Builds the dictionary of fields for HMAC verification in Paymob's documented order.
        /// </summary>
        private static Dictionary<string, string> BuildHmacFields(PaymobWebhookDto obj)
        {
            return new Dictionary<string, string>
            {
                ["amount_cents"] = obj.AmountCents.ToString(),
                ["created_at"] = obj.CreatedAt,
                ["currency"] = obj.Currency,
                ["error_occured"] = obj.ErrorOccured.ToString().ToLower(),
                ["has_parent_transaction"] = obj.HasParentTransaction.ToString().ToLower(),
                ["id"] = obj.Id.ToString(),
                ["integration_id"] = obj.IntegrationId.ToString(),
                ["is_3d_secure"] = obj.Is3dSecure.ToString().ToLower(),
                ["is_auth"] = obj.IsAuth.ToString().ToLower(),
                ["is_capture"] = obj.IsCapture.ToString().ToLower(),
                ["is_refunded"] = obj.IsRefunded.ToString().ToLower(),
                ["is_standalone_payment"] = obj.IsStandalonePayment.ToString().ToLower(),
                ["is_voided"] = obj.IsVoided.ToString().ToLower(),
                ["order"] = obj.Order?.Id.ToString() ?? "",
                ["owner"] = obj.Owner?.ToString() ?? "",
                ["pending"] = obj.Pending.ToString().ToLower(),
                ["source_data.pan"] = obj.SourceData?.Pan ?? "",
                ["source_data.sub_type"] = obj.SourceData?.SubType ?? "",
                ["source_data.type"] = obj.SourceData?.Type ?? "",
                ["success"] = obj.Success.ToString().ToLower()
            };
        }
    }
}