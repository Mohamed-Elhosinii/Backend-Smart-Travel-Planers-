using SmartTravelPlaners.BLL.Features.Subscription.DTOs;

namespace SmartTravelPlaners.BLL.Features.Subscription.Interfaces
{
    public interface ISubscriptionService
    {
        /// <summary>Returns all available plans.</summary>
        Task<IEnumerable<PlanDto>> GetPlansAsync();

        /// <summary>Returns the calling user's active subscription + current usage.</summary>
        Task<SubscriptionDto> GetMySubscriptionAsync(string userId);

        /// <summary>
        /// Creates a subscription for the given plan.
        /// For free plans: activates immediately, returns null.
        /// For paid plans: creates a Paymob order and returns the iframe URL.
        /// </summary>
        Task<string?> CreateSubscriptionAsync(string userId, Guid planId);

        /// <summary>
        /// Called by the webhook after HMAC verification.
        /// Looks up the PaymentTransaction by Paymob order ID and activates the subscription.
        /// </summary>
        Task ActivateSubscriptionAsync(string paymobOrderId, string paymobTransactionId);

        /// <summary>Cancels the user's active subscription.</summary>
        Task CancelSubscriptionAsync(string userId);

        /// <summary>
        /// Creates a Free-plan subscription for a newly registered user.
        /// Called from AuthService after user profile creation.
        /// </summary>
        Task EnsureDefaultSubscriptionAsync(string userId);

        // TODO: Future enhancement — Paymob card tokenization for auto-renewal.
        //       When implemented, add RenewSubscriptionAsync(string userId) that charges
        //       the stored card token without requiring the user to visit the iframe again.
    }
}
