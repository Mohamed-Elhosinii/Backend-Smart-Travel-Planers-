using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;

namespace SmartTravelPlaners.BLL.Features.Hotel.Plugins
{
    public class HotelPlugin
    {
        private readonly IHotelApiService _hotelApiService;
        private readonly IPlaceResolverService _placeResolverService;
        private readonly IHotelSearchService _hotelSearchService;

        public HotelPlugin(
            IHotelApiService hotelApiService,
            IPlaceResolverService placeResolverService,
            IHotelSearchService hotelSearchService)
        {
            _hotelApiService = hotelApiService;
            _placeResolverService = placeResolverService;
            _hotelSearchService = hotelSearchService;
        }

        [KernelFunction("search_hotels")]
        [Description("Search for available hotels in a specific city for given check-in/check-out dates")]
        public async Task<string> SearchHotelsAsync(
            [Description("City name e.g. Cairo, Fayoum")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Number of adults")] int adults = 2,
            [Description("Number of children")] int children = 0)
        {
            if (!DateTime.TryParse(checkIn, out var ci) || !DateTime.TryParse(checkOut, out var co))
                return JsonSerializer.Serialize(new { message = "Invalid check-in or check-out dates." });

            var resolveResult = await _placeResolverService.ResolveAsync(city);
            
            List<SmartTravelPlaners.BLL.Features.Hotel.DTOs.HotelDto> finalHotels;

            if (resolveResult.Status == SmartTravelPlaners.BLL.Features.Hotel.DTOs.ResolutionStatus.Resolved && !string.IsNullOrEmpty(resolveResult.DestId))
            {
                var searchResult = await _hotelSearchService.SearchAsync(
                    resolveResult.DestId, resolveResult.DestType, ci, co, adults, 1);
                
                finalHotels = searchResult.Hotels ?? new List<SmartTravelPlaners.BLL.Features.Hotel.DTOs.HotelDto>();
            }
            else
            {
                var googleHotels = await _hotelApiService.GetAvailableHotelsAsync(city, checkIn, checkOut, adults, children);
                finalHotels = MapGoogleHotelsToCommonDto(googleHotels ?? new List<SmartTravelPlaners.BLL.Features.Hotel.DTOs.GoogleHotelDto>());
            }

            if (finalHotels.Count == 0)
                return JsonSerializer.Serialize(new { message = "No hotels found for this search." });

            return JsonSerializer.Serialize(finalHotels);
        }

        private List<SmartTravelPlaners.BLL.Features.Hotel.DTOs.HotelDto> MapGoogleHotelsToCommonDto(List<SmartTravelPlaners.BLL.Features.Hotel.DTOs.GoogleHotelDto> googleHotels)
        {
            return googleHotels.Select(h => new SmartTravelPlaners.BLL.Features.Hotel.DTOs.HotelDto
            {
                HotelId = h.HotelId,
                Name = h.Name,
                Price = h.Price != null && h.Price.PricePerNight.HasValue ? new SmartTravelPlaners.BLL.Features.Hotel.DTOs.HotelPriceDto
                {
                    Amount = (decimal)h.Price.PricePerNight.Value,
                    Currency = h.Price.Currency ?? "USD",
                    IsEstimated = false
                } : null,
                Rating = h.Rating != null && h.Rating.Value.HasValue ? new SmartTravelPlaners.BLL.Features.Hotel.DTOs.HotelReviewDto
                {
                    Score = h.Rating.Value.Value,
                    ReviewCount = h.Rating.Votes ?? 0,
                    WeightedScore = h.Rating.Value.Value * Math.Log10(Math.Max(h.Rating.Votes ?? 1, 1))
                } : null,
                Images = h.Images ?? new List<string>(),
                Location = new SmartTravelPlaners.BLL.Features.Hotel.DTOs.LocationDto
                {
                    Address = h.Location?.Address ?? "",
                    Latitude = h.Location?.Latitude ?? 0,
                    Longitude = h.Location?.Longitude ?? 0
                },
                Amenities = h.Amenities ?? new List<string>(),
                Stars = h.Stars ?? 0,
                Sources = new List<string> { "GoogleHotels" }
            }).ToList();
        }

        [KernelFunction("get_hotel_details")]
        [Description("Get full details about a specific hotel by its name or ID from previous search results")]
        public async Task<string> GetHotelDetailsAsync(
            [Description("City name used in the original search")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Hotel ID or Name from previous search results")] string hotelId)
        {
            var hotel = await _hotelApiService.GetHotelByIdAsync(city, checkIn, checkOut, hotelId);

            return hotel is null
                ? JsonSerializer.Serialize(new { error = "Hotel not found" })
                : JsonSerializer.Serialize(hotel);
        }

        [KernelFunction("filter_hotels")]
        [Description("Filter hotels in a city by maximum price per night, minimum rating, or required amenities like pool, wifi, breakfast")]
        public async Task<string> FilterHotelsAsync(
            [Description("City name")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Maximum price per night in USD")] decimal maxPrice,
            [Description("Minimum rating from 1 to 5")] double minRating,
            [Description("Required amenities e.g. pool, wifi, breakfast")] List<string> amenities)
        {
            var hotels = await _hotelApiService.FilterHotelsAsync(city, checkIn, checkOut, maxPrice, minRating, amenities);

            if (hotels.Count == 0)
                return JsonSerializer.Serialize(new { message = "No hotels matched these filters." });

            return JsonSerializer.Serialize(hotels);
        }

        [KernelFunction("get_hotels_near_location")]
        [Description("Find hotels near specific coordinates (e.g. a landmark) within a city, sorted by distance")]
        public async Task<string> GetHotelsNearLocationAsync(
            [Description("City name (used for the underlying search)")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Latitude of the target location")] double latitude,
            [Description("Longitude of the target location")] double longitude,
            [Description("Search radius in kilometers")] int radiusKm)
        {
            var hotels = await _hotelApiService.GetHotelsNearLocationAsync(city, checkIn, checkOut, latitude, longitude, radiusKm);

            if (hotels.Count == 0)
                return JsonSerializer.Serialize(new { message = "No hotels found within this radius." });

            return JsonSerializer.Serialize(hotels);
        }

        [KernelFunction("get_similar_hotels")]
        [Description("Get hotels similar in price and rating to a specific hotel the user liked")]
        public async Task<string> GetSimilarHotelsAsync(
            [Description("City name (used for the underlying search)")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Hotel ID or Name to find similar ones for")] string hotelId)
        {
            var hotels = await _hotelApiService.GetSimilarHotelsAsync(city, checkIn, checkOut, hotelId);

            if (hotels.Count == 0)
                return JsonSerializer.Serialize(new { message = "No similar hotels found." });

            return JsonSerializer.Serialize(hotels);
        }

        [KernelFunction("check_hotel_availability")]
        [Description("Check if a specific hotel appears in availability results for given dates")]
        public async Task<string> CheckAvailabilityAsync(
            [Description("City name")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Hotel ID or Name")] string hotelId)
        {
            var available = await _hotelApiService.CheckAvailabilityAsync(city, checkIn, checkOut, hotelId);
            return available ? "Hotel is available" : "Hotel is not available on these dates";
        }
    }
}
