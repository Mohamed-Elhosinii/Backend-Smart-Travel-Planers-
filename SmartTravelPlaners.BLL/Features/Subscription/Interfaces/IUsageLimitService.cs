namespace SmartTravelPlaners.BLL.Features.Subscription.Interfaces
{
    public interface IUsageLimitService
    {
        /// <summary>Returns true if the user hasn't exceeded their monthly trip limit.</summary>
        Task<bool> CanGenerateTripAsync(string userId);

        /// <summary>Returns true if the user hasn't exceeded their monthly message limit.</summary>
        Task<bool> CanSendMessageAsync(string userId);

        /// <summary>Increments the trip counter for the current month.</summary>
        Task IncrementTripUsageAsync(string userId);

        /// <summary>Increments the message counter for the current month.</summary>
        Task IncrementMessageUsageAsync(string userId);

        /// <summary>Returns current usage and limits for the user.</summary>
        Task<(int TripsUsed, int? TripsLimit, int MessagesUsed, int? MessagesLimit)> GetCurrentUsageAsync(string userId);

        /// <summary>Resets the usage counters for the current month.</summary>
        Task ResetUsageAsync(string userId);
    }
}
