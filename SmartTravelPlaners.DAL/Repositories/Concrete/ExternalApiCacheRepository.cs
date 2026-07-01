using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class ExternalApiCacheRepository : IExternalApiCacheRepository
    {
        private readonly ApplicationDbContext _context;

        public ExternalApiCacheRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetAsync(string cacheKey, string source)
        {
            var entry = await _context.ExternalApiCaches
                .FirstOrDefaultAsync(e => e.CacheKey == cacheKey && e.Source == source);

            if (entry == null) return null;

            if (entry.ExpiresAt < DateTime.UtcNow)
            {
                _context.ExternalApiCaches.Remove(entry);
                return null;
            }

            return entry.PayloadJson;
        }

        public async Task SetAsync(string cacheKey, string source, string payload, TimeSpan ttl)
        {
            var existing = await _context.ExternalApiCaches
                .FirstOrDefaultAsync(e => e.CacheKey == cacheKey && e.Source == source);

            if (existing != null)
            {
                existing.PayloadJson = payload;
                existing.ExpiresAt = DateTime.UtcNow.Add(ttl);
                _context.ExternalApiCaches.Update(existing);
            }
            else
            {
                await _context.ExternalApiCaches.AddAsync(new ExternalApiCache
                {
                    CacheKey = cacheKey,
                    Source = source,
                    PayloadJson = payload,
                    ExpiresAt = DateTime.UtcNow.Add(ttl)
                });
            }
        }

        public async Task CleanExpiredAsync()
        {
            var expired = await _context.ExternalApiCaches
                .Where(e => e.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (expired.Count > 0)
            {
                _context.ExternalApiCaches.RemoveRange(expired);
            }
        }
    }
}
