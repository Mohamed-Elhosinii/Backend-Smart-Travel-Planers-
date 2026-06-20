using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.DAL.Configurations;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Subscription.Services
{
    public class UsageLimitService : IUsageLimitService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UsageLimitService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> CanGenerateTripAsync(string userId)
        {
            var (tripsUsed, tripsLimit, _, _) = await GetCurrentUsageAsync(userId);

            // null limit = unlimited
            if (tripsLimit == null) return true;

            return tripsUsed < tripsLimit.Value;
        }

        public async Task<bool> CanSendMessageAsync(string userId)
        {
            var (_, _, messagesUsed, messagesLimit) = await GetCurrentUsageAsync(userId);

            // null limit = unlimited
            if (messagesLimit == null) return true;

            return messagesUsed < messagesLimit.Value;
        }

        public async Task IncrementTripUsageAsync(string userId)
        {
            var counter = await GetOrCreateCounterAsync(userId);
            counter.TripsUsed++;
            _unitOfWork.Repository<UsageCounter>().Update(counter);
            await _unitOfWork.CompleteAsync();
        }

        public async Task IncrementMessageUsageAsync(string userId)
        {
            var counter = await GetOrCreateCounterAsync(userId);
            counter.MessagesUsed++;
            _unitOfWork.Repository<UsageCounter>().Update(counter);
            await _unitOfWork.CompleteAsync();
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
