using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.DAL.Context;

namespace SmartTravelPlaners.BLL.Features.Auth.Jobs
{
    public class TokenCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenCleanupJob> _logger;

        public TokenCleanupJob(IServiceProvider serviceProvider, ILogger<TokenCleanupJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TokenCleanupJob background service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupTokensAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing TokenCleanupJob.");
                }

                // Run once a day
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }

            _logger.LogInformation("TokenCleanupJob background service is stopping.");
        }

        private async Task CleanupTokensAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            // Delete tokens that are either revoked or expired
            var deletedCount = await context.RefreshTokens
                .Where(t => t.IsRevoked || t.ExpiresAt < now)
                .ExecuteDeleteAsync(stoppingToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("TokenCleanupJob: Deleted {Count} expired/revoked refresh tokens.", deletedCount);
            }
        }
    }
}
