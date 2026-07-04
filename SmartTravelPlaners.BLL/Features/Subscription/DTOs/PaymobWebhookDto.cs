using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Subscription.DTOs
{
    /// <summary>
    /// Matches the "obj" part of Paymob's transaction webhook payload.
    /// Only the fields we need are mapped; the rest are ignored by the serializer.
    /// </summary>
    public class PaymobWebhookDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("pending")]
        public bool Pending { get; set; }

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("is_auth")]
        public bool IsAuth { get; set; }

        [JsonPropertyName("is_capture")]
        public bool IsCapture { get; set; }

        [JsonPropertyName("is_standalone_payment")]
        public bool IsStandalonePayment { get; set; }

        [JsonPropertyName("is_voided")]
        public bool IsVoided { get; set; }

        [JsonPropertyName("is_refunded")]
        public bool IsRefunded { get; set; }

        [JsonPropertyName("is_3d_secure")]
        public bool Is3dSecure { get; set; }

        [JsonPropertyName("integration_id")]
        public int IntegrationId { get; set; }

        [JsonPropertyName("has_parent_transaction")]
        public bool HasParentTransaction { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("error_occured")]
        public bool ErrorOccured { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("order")]
        public PaymobOrderDto? Order { get; set; }

        [JsonPropertyName("source_data")]
        public PaymobSourceDataDto? SourceData { get; set; }

        [JsonPropertyName("merchant_order_id")]
        public string MerchantOrderId { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public object? Owner { get; set; }
    }

    public class PaymobOrderDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class PaymobSourceDataDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("sub_type")]
        public string SubType { get; set; } = string.Empty;

        [JsonPropertyName("pan")]
        public string Pan { get; set; } = string.Empty;
    }

    /// <summary>
    /// Root wrapper for the webhook callback body.
    /// </summary>
    public class PaymobWebhookRootDto
    {
        [JsonPropertyName("obj")]
        public PaymobWebhookDto? Obj { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("hmac")]
        public string Hmac { get; set; } = string.Empty;
    }
}
