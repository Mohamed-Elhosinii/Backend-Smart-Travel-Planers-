using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.BLL.Features.Subscription.Interfaces
{
    public interface IPaymobService
    {
        /// <summary>Step 1: Authenticate with Paymob and get an auth token.</summary>
        Task<string> AuthenticateAsync();

        /// <summary>Step 2: Create an order on Paymob.</summary>
        Task<int> CreateOrderAsync(int amountCents, string authToken, string merchantOrderId);

        /// <summary>Step 3: Get a payment key for the iframe.</summary>
        Task<string> GetPaymentKeyAsync(int orderId, int amountCents, string authToken, UserProfile userProfile);

        /// <summary>
        /// Combines the 3-step flow (auth → order → payment key) and returns
        /// the full iframe URL the client should redirect to.
        /// </summary>
        Task<string> InitiatePaymentAsync(UserProfile userProfile, Plan plan, Guid subscriptionId, string paymobOrderId);

        /// <summary>
        /// Verifies the HMAC signature on a Paymob webhook payload.
        /// Uses SHA-512 per Paymob's documented field concatenation order.
        /// </summary>
        bool VerifyHmac(Dictionary<string, string> transactionFields, string receivedHmac);

        /// <summary>
        /// Performs a refund for a successful transaction via Paymob's Refund API.
        /// </summary>
        Task<bool> RefundPaymentAsync(string transactionId, decimal amount);
    }
}
