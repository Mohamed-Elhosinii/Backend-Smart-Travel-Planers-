using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.DAL.Configurations;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Subscription.Services
{
    public class UsageLimitService : IUsageLimitService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UsageLimitService> _logger;

        public UsageLimitService(IUnitOfWork unitOfWork, ILogger<UsageLimitService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<bool> CanGenerateTripAsync(string userId)
        {
            try
            {
                var (tripsUsed, tripsLimit, _, _) = await GetCurrentUsageAsync(userId);

                // null limit = unlimited
                if (tripsLimit == null)
                {
                    _logger.LogInformation("Trip generation check for UserId: {UserId} - Unlimited plan", userId);
                    return true;
                }

                var canGenerate = tripsUsed < tripsLimit.Value;

                if (canGenerate)
                {
                    _logger.LogInformation("Trip generation allowed for UserId: {UserId}. Used: {TripsUsed}/{TripsLimit}", userId, tripsUsed, tripsLimit);
                }
                else
                {
                    _logger.LogWarning("Trip generation limit reached for UserId: {UserId}. Used: {TripsUsed}/{TripsLimit}", userId, tripsUsed, tripsLimit);
                }

                return canGenerate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking trip generation eligibility for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task<bool> CanSendMessageAsync(string userId)
        {
            try
            {
                var (_, _, messagesUsed, messagesLimit) = await GetCurrentUsageAsync(userId);

                // null limit = unlimited
                if (messagesLimit == null)
                {
                    _logger.LogInformation("Message send check for UserId: {UserId} - Unlimited plan", userId);
                    return true;
                }

                var canSend = messagesUsed < messagesLimit.Value;

                if (canSend)
                {
                    _logger.LogInformation("Message send allowed for UserId: {UserId}. Used: {MessagesUsed}/{MessagesLimit}", userId, messagesUsed, messagesLimit);
                }
                else
                {
                    _logger.LogWarning("Message send limit reached for UserId: {UserId}. Used: {MessagesUsed}/{MessagesLimit}", userId, messagesUsed, messagesLimit);
                }

                return canSend;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking message send eligibility for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task IncrementTripUsageAsync(string userId)
        {
            try
            {
                var counter = await GetOrCreateCounterAsync(userId);
                counter.TripsUsed++;
                _unitOfWork.Repository<UsageCounter>().Update(counter);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Trip usage incremented for UserId: {UserId}. New count: {TripsUsed}", userId, counter.TripsUsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to increment trip usage for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task IncrementMessageUsageAsync(string userId)
        {
            try
            {
                var counter = await GetOrCreateCounterAsync(userId);
                counter.MessagesUsed++;
                _unitOfWork.Repository<UsageCounter>().Update(counter);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Message usage incremented for UserId: {UserId}. New count: {MessagesUsed}", userId, counter.MessagesUsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to increment message usage for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task<(int TripsUsed, int? TripsLimit, int MessagesUsed, int? MessagesLimit)>
            GetCurrentUsageAsync(string userId)
        {
            var plan = await GetActivePlanAsync(userId);
            var counter = await GetOrCreateCounterAsync(userId);

            return (
                counter.TripsUsed,
                plan.MaxTripsPerMonth,
                counter.MessagesUsed,
                plan.MaxMessagesPerMonth
            );
        }

        public async Task ResetUsageAsync(string userId)
        {
            try
            {
                var counter = await GetOrCreateCounterAsync(userId);
                counter.TripsUsed = 0;
                counter.MessagesUsed = 0;
                _unitOfWork.Repository<UsageCounter>().Update(counter);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Usage counters reset for UserId: {UserId}.", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset usage counters for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        // =================================================================
        // Private helpers
        // =================================================================

        private async Task<Plan> GetActivePlanAsync(string userId)
        {
            var userProfile = await GetUserProfileAsync(userId);

            // Find active subscription with plan loaded
            var subscriptions = await _unitOfWork.Repository<DAL.Entities.Subscription>()
                .FindAsync(s => s.UserProfileId == userProfile.Id
                             && s.Status == DAL.Enums.SubscriptionStatus.Active);

            var activeSub = subscriptions.FirstOrDefault();

            if (activeSub != null)
            {
                var plan = await _unitOfWork.Repository<Plan>().GetByIdAsync(activeSub.PlanId);
                if (plan != null) return plan;
            }

            // Fallback to Free plan
            var freePlan = await _unitOfWork.Repository<Plan>().GetByIdAsync(PlanConfiguration.FreePlanId);
            return freePlan ?? throw new Exception("Free plan not found in database. Ensure seed data exists.");
        }

        private async Task<UserProfile> GetUserProfileAsync(string userId)
        {
            var profiles = await _unitOfWork.UserProfiles
                .FindAsync(p => p.AspNetUserId == userId);

            return profiles.FirstOrDefault()
                   ?? throw new Exception($"UserProfile not found for AspNetUserId: {userId}");
        }

        private async Task<UsageCounter> GetOrCreateCounterAsync(string userId)
        {
            var userProfile = await GetUserProfileAsync(userId);
            var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

            var counters = await _unitOfWork.Repository<UsageCounter>()
                .FindAsync(uc => uc.UserProfileId == userProfile.Id
                              && uc.PeriodMonth == currentMonth);

            var counter = counters.FirstOrDefault();

            if (counter == null)
            {
                counter = new UsageCounter
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = userProfile.Id,
                    PeriodMonth = currentMonth,
                    TripsUsed = 0,
                    MessagesUsed = 0
                };

                await _unitOfWork.Repository<UsageCounter>().AddAsync(counter);
                await _unitOfWork.CompleteAsync();
            }

            return counter;
        }
    }
}
