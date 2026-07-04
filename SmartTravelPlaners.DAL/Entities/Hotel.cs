using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.DAL.Entities
{
    public class Hotel
    {
        public Guid Id { get; set; }

        // FK → Trip 
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public DateOnly CheckIn { get; set; }
        public DateOnly CheckOut { get; set; }
        public decimal PricePerNight { get; set; }
        public string? BookingUrl { get; set; }
        public int Stars { get; set; }              
        public BookingStatus Status { get; set; } = BookingStatus.Suggested;
        public string? ImagesJson { get; set; }
    }
}
