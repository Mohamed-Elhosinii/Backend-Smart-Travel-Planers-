namespace SmartTravelPlaners.DAL.Entities
{
    
    public class PlaceCache
    {
        public Guid Id { get; set; }
        public string PlaceId { get; set; } = string.Empty;    
        public string Name { get; set; } = string.Empty;
        public string? Type { get; set; }                     
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public float? Rating { get; set; }
        public string? Description { get; set; }
        public string? PhotosJson { get; set; }                
        public string? OpeningHoursJson { get; set; }         
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    }
}
