
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.DAL.Configurations;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Subscription.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymobService _paymobService;
        private readonly IUsageLimitService _usageLimitService;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            IUnitOfWork unitOfWork,
            IPaymobService paymobService,
            IUsageLimitService usageLimitService,
            ILogger<SubscriptionService> logger)
        {
            _unitOfWork = unitOfWork;
            _paymobService = paymobService;
            _usageLimitService = usageLimitService;
            _logger = logger;
        }

        // =====================================================================
        // Get all plans
        // =====================================================================
        public async Task<IEnumerable<PlanDto>> GetPlansAsync()
        {
            var plans = await _unitOfWork.Repository<Plan>().GetAllAsync();

            return plans.Select(p => new PlanDto
            {
                Id = p.Id,
                Name = p.Name,
                PriceMonthly = p.PriceMonthly,
                MaxTripsPerMonth = p.MaxTripsPerMonth,
                MaxMessagesPerMonth = p.MaxMessagesPerMonth
            });
        }

        // =====================================================================
        // Get my subscription + usage
        // =====================================================================
        public async Task<SubscriptionDto> GetMySubscriptionAsync(string userId)
        {
            var userProfile = await GetUserProfileAsync(userId);

            var subscriptions = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id
                             && s.Status == SubscriptionStatus.Active);

            var activeSub = subscriptions.FirstOrDefault();

            Plan plan;
            SubscriptionStatus status;
            DateTime periodEnd;

            if (activeSub != null)
            {
                _logger.LogInformation("Active subscription found for user {UserId}. Plan: {PlanId}, Status: {Status}",
                    userId, activeSub.PlanId, activeSub.Status);

                plan = await _unitOfWork.Repository<Plan>().GetByIdAsync(activeSub.PlanId)
                       ?? await GetFreePlanAsync();
                status = activeSub.Status;
                periodEnd = activeSub.CurrentPeriodEnd;
            }
            else
            {
                _logger.LogWarning("No active subscription found for user {UserId}. Falling back to Free plan.", userId);

                // No active subscription — show Free plan info
                plan = await GetFreePlanAsync();
                status = SubscriptionStatus.Active;
                periodEnd = DateTime.UtcNow;
            }

            var (tripsUsed, tripsLimit, messagesUsed, messagesLimit) =
                await _usageLimitService.GetCurrentUsageAsync(userId);

            return new SubscriptionDto
            {
                PlanName = plan.Name,
                Status = status.ToString(),
                CurrentPeriodEnd = periodEnd,
                TripsUsed = tripsUsed,
                TripsLimit = tripsLimit,
                MessagesUsed = messagesUsed,
                MessagesLimit = messagesLimit
            };
        }

        // =====================================================================
        // Create subscription (subscribe/upgrade)
        // =====================================================================
        public async Task<string?> CreateSubscriptionAsync(string userId, Guid planId)
        {
            _logger.LogInformation("Starting subscription creation for user {UserId} with plan {PlanId}.",
                userId, planId);

            var plan = await _unitOfWork.Repository<Plan>().GetByIdAsync(planId)
                       ?? throw new Exception("Plan not found");

            var userProfile = await GetUserProfileAsync(userId);

            // Cancel any existing active subscription first
            var existingSubs = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id
                             && s.Status == SubscriptionStatus.Active);

            foreach (var existing in existingSubs)
            {
                _logger.LogInformation("Cancelling existing subscription {SubscriptionId} for user {UserId}.",
                    existing.Id, userId);

                existing.Status = SubscriptionStatus.Cancelled;
                _unitOfWork.Repository<DAL.Entities.Subscription>().Update(existing);
            }

            var now = DateTime.UtcNow;
            var subscription = new DAL.Entities.Subscription
            {
                Id = Guid.NewGuid(),
                UserProfileId = userProfile.Id,
                PlanId = planId,
                Status = plan.PriceMonthly == 0
                    ? SubscriptionStatus.Active
                    : SubscriptionStatus.PastDue, // Activated by webhook after payment
                CurrentPeriodStart = now,
                CurrentPeriodEnd = now.AddMonths(1)
            };

            await _unitOfWork.Repository<DAL.Entities.Subscription>().AddAsync(subscription);

            // Free plan: activate immediately, no payment needed
            if (plan.PriceMonthly == 0)
            {
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Free subscription {SubscriptionId} created successfully for user {UserId}.",
                    subscription.Id, userId);

                return null;
            }

            // Paid plan: create payment transaction BEFORE redirecting to Paymob
            // This maps (PaymobOrderId → userId + planId) so the webhook can look up who paid
            var merchantOrderId = $"SUB-{subscription.Id}";

            var paymentTransaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                SubscriptionId = subscription.Id,
                PaymobOrderId = merchantOrderId,
                Amount = plan.PriceMonthly,
                Status = "pending",
                CreatedAt = now
            };

            await _unitOfWork.Repository<PaymentTransaction>().AddAsync(paymentTransaction);
            await _unitOfWork.CompleteAsync();

            // Initiate Paymob payment flow
            try
            {
                var iframeUrl = await _paymobService.InitiatePaymentAsync(
                    userId, plan, subscription.Id, merchantOrderId);

                _logger.LogInformation("Paid subscription {SubscriptionId} created successfully for user {UserId}. Payment initiated.",
                    subscription.Id, userId);

                return iframeUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate payment for subscription {SubscriptionId} and user {UserId}. Error: {ErrorMessage}",
                    subscription.Id, userId, ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Activate subscription (called by webhook)
        // =====================================================================
        public async Task ActivateSubscriptionAsync(string paymobOrderId, string paymobTransactionId)
        {
            _logger.LogInformation("Starting subscription activation for Paymob order {OrderId}.", paymobOrderId);

            var transactions = await _unitOfWork.Repository<PaymentTransaction>()
                .FindAsync(pt => pt.PaymobOrderId == paymobOrderId);

            var transaction = transactions.FirstOrDefault();
            if (transaction == null)
            {
                _logger.LogWarning("PaymentTransaction not found for Paymob order {OrderId}.", paymobOrderId);
                throw new Exception($"PaymentTransaction not found for order: {paymobOrderId}");
            }

            // Update the payment transaction
            transaction.PaymobTransactionId = paymobTransactionId;
            transaction.Status = "paid";
            _unitOfWork.Repository<PaymentTransaction>().Update(transaction);

            // Activate the subscription
            var subscription = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .GetByIdAsync(transaction.SubscriptionId);

            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for transaction {TransactionId}.", transaction.Id);
                throw new Exception($"Subscription not found: {transaction.SubscriptionId}");
            }

            var now = DateTime.UtcNow;
            subscription.Status = SubscriptionStatus.Active;
            subscription.CurrentPeriodStart = now;
            subscription.CurrentPeriodEnd = now.AddMonths(1);
            _unitOfWork.Repository<DAL.Entities.Subscription>().Update(subscription);

            try
            {
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Subscription {SubscriptionId} activated successfully for Paymob order {OrderId}.",
                    subscription.Id, paymobOrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate subscription {SubscriptionId} for Paymob order {OrderId}. Error: {ErrorMessage}",
                    subscription.Id, paymobOrderId, ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Cancel subscription
        // =====================================================================
        public async Task CancelSubscriptionAsync(string userId)
        {
            _logger.LogInformation("Starting subscription cancellation for user {UserId}.", userId);

            var userProfile = await GetUserProfileAsync(userId);

            var subscriptions = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id
                             && s.Status == SubscriptionStatus.Active);

            if (!subscriptions.Any())
            {
                _logger.LogWarning("No active subscription found to cancel for user {UserId}.", userId);
            }

            foreach (var sub in subscriptions)
            {
                // Find any successful payment transaction for this subscription
                var transactions = await _unitOfWork.Repository<PaymentTransaction>()
                    .FindAsync(pt => pt.SubscriptionId == sub.Id && pt.Status == "paid");

                var paidTransaction = transactions.FirstOrDefault();
                if (paidTransaction != null && !string.IsNullOrEmpty(paidTransaction.PaymobTransactionId))
                {
                    // Refund via Paymob
                    await _paymobService.RefundPaymentAsync(paidTransaction.PaymobTransactionId, paidTransaction.Amount);

                    // Mark transaction as refunded
                    paidTransaction.Status = "refunded";
                    _unitOfWork.Repository<PaymentTransaction>().Update(paidTransaction);

                    _logger.LogInformation("Refund processed for subscription {SubscriptionId} and user {UserId}.",
                        sub.Id, userId);
                }

                sub.Status = SubscriptionStatus.Cancelled;
                _unitOfWork.Repository<DAL.Entities.Subscription>().Update(sub);

                _logger.LogInformation("Subscription {SubscriptionId} cancelled for user {UserId}.", sub.Id, userId);
            }

            try
            {
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Subscription cancellation completed successfully for user {UserId}.", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete subscription cancellation for user {UserId}. Error: {ErrorMessage}",
                    userId, ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Ensure default Free subscription for new users
        // =====================================================================
        public async Task EnsureDefaultSubscriptionAsync(string userId)
        {
            _logger.LogInformation("Ensuring default Free subscription for user {UserId}.", userId);

            var userProfile = await GetUserProfileAsync(userId);

            // Check if user already has a subscription
            var existing = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id);

            if (existing.Any())
            {
                _logger.LogInformation("User {UserId} already has subscription(s). Skipping default subscription creation.", userId);
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                var subscription = new DAL.Entities.Subscription
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = userProfile.Id,
                    PlanId = PlanConfiguration.FreePlanId,
                    Status = SubscriptionStatus.Active,
                    CurrentPeriodStart = now,
                    CurrentPeriodEnd = now.AddYears(100) // Free plan never expires
                };

                await _unitOfWork.Repository<DAL.Entities.Subscription>().AddAsync(subscription);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Default Free subscription {SubscriptionId} created successfully for user {UserId}.",
                    subscription.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default subscription for user {UserId}. Error: {ErrorMessage}",
                    userId, ex.Message);
                throw;
            }
        }

        // =====================================================================
        // Private helpers
        // =====================================================================

        private async Task<UserProfile> GetUserProfileAsync(string userId)
        {
            var profiles = await _unitOfWork.UserProfiles
                .FindAsync(p => p.AspNetUserId == userId);

            return profiles.FirstOrDefault()
                   ?? throw new Exception($"UserProfile not found for AspNetUserId: {userId}");
        }

        private async Task<Plan> GetFreePlanAsync()
        {
            return await _unitOfWork.Repository<Plan>().GetByIdAsync(PlanConfiguration.FreePlanId)
                   ?? throw new Exception("Free plan not found in database");
        }
    }
}
