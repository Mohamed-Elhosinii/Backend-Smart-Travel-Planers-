using System;

namespace SmartTravelPlaners.DAL.Entities
{
    public class ExternalApiCache
    {
        public Guid Id { get; set; }
        public string CacheKey { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
    }
}
