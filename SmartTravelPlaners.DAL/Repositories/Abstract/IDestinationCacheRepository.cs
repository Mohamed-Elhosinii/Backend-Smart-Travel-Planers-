using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Repositories.Abstract
{
    public interface IDestinationCacheRepository
    {
        Task<PlaceCache?> GetByNormalizedQueryAsync(string normalizedQuery);
        Task<List<(string NormalizedQuery, string ResolvedName, string DestId)>> GetAllForFuzzyMatchAsync();
        Task AddAsync(PlaceCache entry);
        Task UpdateLastUsedAtAsync(Guid id);
    }
}
