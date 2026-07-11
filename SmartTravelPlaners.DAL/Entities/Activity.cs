using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.DAL.Entities
{
    public class Activity
    {
        public Guid Id { get; set; }

        // FK → TripDay
        public Guid TripDayId { get; set; }
        public TripDay TripDay { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public ActivityType Type { get; set; }
        public string? LocationName { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public string TimeSlot { get; set; } = "Morning";   // Morning | Afternoon | Evening | Night
        public TimeOnly? StartTime { get; set; }
        public decimal EstimatedCost { get; set; }
        public string? BookingUrl { get; set; }
        public ActivityStatus Status { get; set; } = ActivityStatus.Suggested;
        public string? Notes { get; set; }
        
        public double? Rating { get; set; }
        public string? Address { get; set; }
        public string? ImageUrl { get; set; }

        // Soft reference to Places Cache (not a FK)
        public string? PlaceId { get; set; }    // Google Places ID
    }
}
