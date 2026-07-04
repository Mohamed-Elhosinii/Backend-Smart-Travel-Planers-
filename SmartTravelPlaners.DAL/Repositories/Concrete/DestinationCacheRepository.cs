using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class DestinationCacheRepository : IDestinationCacheRepository
    {
        private readonly ApplicationDbContext _context;

        public DestinationCacheRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PlaceCache?> GetByNormalizedQueryAsync(string normalizedQuery)
        {
            return await _context.PlacesCache
                .FirstOrDefaultAsync(p => p.NormalizedQuery == normalizedQuery);
        }

        public async Task<List<(string NormalizedQuery, string ResolvedName, string DestId)>> GetAllForFuzzyMatchAsync()
        {
            return await _context.PlacesCache
                .Where(p => p.NormalizedQuery != null)
                .Select(p => new Tuple<string, string, string>(p.NormalizedQuery!, p.Name, p.PlaceId).ToValueTuple())
                .ToListAsync();
        }

        public async Task AddAsync(PlaceCache entry)
        {
            await _context.PlacesCache.AddAsync(entry);
        }

        public async Task UpdateLastUsedAtAsync(Guid id)
        {
            var entry = await _context.PlacesCache.FindAsync(id);
            if (entry != null)
            {
                entry.LastUsedAt = DateTime.UtcNow;
            }
        }
    }
}
