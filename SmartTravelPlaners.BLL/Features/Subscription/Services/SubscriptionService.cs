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

        public SubscriptionService(
            IUnitOfWork unitOfWork,
            IPaymobService paymobService,
            IUsageLimitService usageLimitService)
        {
            _unitOfWork = unitOfWork;
            _paymobService = paymobService;
            _usageLimitService = usageLimitService;
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
                plan = await _unitOfWork.Repository<Plan>().GetByIdAsync(activeSub.PlanId)
                       ?? await GetFreePlanAsync();
                status = activeSub.Status;
                periodEnd = activeSub.CurrentPeriodEnd;
            }
            else
            {
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
            var plan = await _unitOfWork.Repository<Plan>().GetByIdAsync(planId)
                       ?? throw new Exception("Plan not found");

            var userProfile = await GetUserProfileAsync(userId);

            // Cancel any existing active subscription first
            var existingSubs = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id
                             && s.Status == SubscriptionStatus.Active);

            foreach (var existing in existingSubs)
            {
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
            var iframeUrl = await _paymobService.InitiatePaymentAsync(
                userId, plan, subscription.Id, merchantOrderId);

            return iframeUrl;
        }

        // =====================================================================
        // Activate subscription (called by webhook)
        // =====================================================================
        public async Task ActivateSubscriptionAsync(string paymobOrderId, string paymobTransactionId)
        {
            var transactions = await _unitOfWork.Repository<PaymentTransaction>()
                .FindAsync(pt => pt.PaymobOrderId == paymobOrderId);

            var transaction = transactions.FirstOrDefault()
                              ?? throw new Exception($"PaymentTransaction not found for order: {paymobOrderId}");

            // Update the payment transaction
            transaction.PaymobTransactionId = paymobTransactionId;
            transaction.Status = "paid";
            _unitOfWork.Repository<PaymentTransaction>().Update(transaction);

            // Activate the subscription
            var subscription = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .GetByIdAsync(transaction.SubscriptionId)
                ?? throw new Exception($"Subscription not found: {transaction.SubscriptionId}");

            var now = DateTime.UtcNow;
            subscription.Status = SubscriptionStatus.Active;
            subscription.CurrentPeriodStart = now;
            subscription.CurrentPeriodEnd = now.AddMonths(1);
            _unitOfWork.Repository<DAL.Entities.Subscription>().Update(subscription);

            await _unitOfWork.CompleteAsync();
        }

        // =====================================================================
        // Cancel subscription
        // =====================================================================
        public async Task CancelSubscriptionAsync(string userId)
        {
            var userProfile = await GetUserProfileAsync(userId);

            var subscriptions = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id
                             && s.Status == SubscriptionStatus.Active);

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
                }

                sub.Status = SubscriptionStatus.Cancelled;
                _unitOfWork.Repository<DAL.Entities.Subscription>().Update(sub);
            }

            await _unitOfWork.CompleteAsync();
        }

        // =====================================================================
        // Ensure default Free subscription for new users
        // =====================================================================
        public async Task EnsureDefaultSubscriptionAsync(string userId)
        {
            var userProfile = await GetUserProfileAsync(userId);

            // Check if user already has a subscription
            var existing = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id);

            if (existing.Any()) return;

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
