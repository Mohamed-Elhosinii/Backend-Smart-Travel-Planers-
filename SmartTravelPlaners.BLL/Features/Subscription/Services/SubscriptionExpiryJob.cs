using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Subscription.Services
{
    /// <summary>
    /// Background service that runs daily to expire subscriptions
    /// whose CurrentPeriodEnd has passed. Expired users fall back to
    /// Free plan limits automatically via UsageLimitService.
    /// </summary>
    public class SubscriptionExpiryJob : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionExpiryJob> _logger;
        private Timer? _timer;

        public SubscriptionExpiryJob(
            IServiceScopeFactory scopeFactory,
            ILogger<SubscriptionExpiryJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SubscriptionExpiryJob starting. Will run daily.");

            // Run once immediately on startup, then every 24 hours
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(24));

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            var jobStartTime = DateTime.UtcNow;
            _logger.LogInformation("SubscriptionExpiryJob started execution at {StartTime}.", jobStartTime);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var now = DateTime.UtcNow;

                _logger.LogInformation("Searching for expired subscriptions with CurrentPeriodEnd before {CheckTime}.", now);

                var expiredSubs = await unitOfWork.Repository<DAL.Entities.Subscription>()
                    .FindAsync(s => s.CurrentPeriodEnd < now
                                 && s.Status == SubscriptionStatus.Active);

                var expiredList = expiredSubs.ToList();

                _logger.LogInformation("Found {ExpiredCount} expired subscription(s) to process.", expiredList.Count);

                if (expiredList.Count == 0)
                {
                    _logger.LogInformation("No expired subscriptions to process. SubscriptionExpiryJob completed successfully at {CompletionTime}.", DateTime.UtcNow);
                    return;
                }

                foreach (var sub in expiredList)
                {
                    sub.Status = SubscriptionStatus.Expired;
                    unitOfWork.Repository<DAL.Entities.Subscription>().Update(sub);

                    _logger.LogInformation(
                        "Subscription {SubscriptionId} marked as expired. UserProfile: {UserProfileId}, Period ended: {PeriodEnd}.",
                        sub.Id, sub.UserProfileId, sub.CurrentPeriodEnd);
                }

                await unitOfWork.CompleteAsync();

                _logger.LogInformation(
                    "SubscriptionExpiryJob completed successfully. Marked {ExpiredCount} subscription(s) as expired. Completion time: {CompletionTime}.",
                    expiredList.Count, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubscriptionExpiryJob failed during execution. Error: {ErrorMessage}. Execution time: {FailureTime}.",
                    ex.Message, DateTime.UtcNow);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SubscriptionExpiryJob stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
