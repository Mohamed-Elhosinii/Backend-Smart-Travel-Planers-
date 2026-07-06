using System;
using System.Threading.Tasks;

namespace SmartTravelPlaners.DAL.Repositories.Abstract
{
    public interface IExternalApiCacheRepository
    {
        Task<string?> GetAsync(string cacheKey, string source);
        Task SetAsync(string cacheKey, string source, string payload, TimeSpan ttl);
        Task CleanExpiredAsync();
    }
}
