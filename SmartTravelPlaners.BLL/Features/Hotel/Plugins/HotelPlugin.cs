using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;

namespace SmartTravelPlaners.BLL.Features.Hotel.Plugins
{
    public class HotelPlugin
    {
        private readonly IHotelApiService _hotelApiService;
        public HotelPlugin(IHotelApiService hotelApiService)
        {
            _hotelApiService = hotelApiService;
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

            var googleHotels = await _hotelApiService.GetAvailableHotelsAsync(city, checkIn, checkOut, adults, children);
            var finalHotels = googleHotels ?? new List<SmartTravelPlaners.BLL.Features.Hotel.DTOs.GoogleHotelDto>();

            if (finalHotels.Count == 0)
                return JsonSerializer.Serialize(new { message = "No hotels found for this search." });

            return SerializePrunedHotels(finalHotels);
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
            if (hotel is null)
                return JsonSerializer.Serialize(new { error = "Hotel not found" });

            var pruned = new {
                hotel_id = hotel.HotelId,
                name = hotel.Name,
                price = hotel.Price?.PricePerNight,
                rating = hotel.Rating?.Value,
                stars = hotel.Stars,
                address = hotel.Location?.Address,
                lat = hotel.Location?.Latitude,
                lng = hotel.Location?.Longitude,
                description = hotel.Description,
                amenities = hotel.Amenities,
                images = hotel.Images?.Take(3).ToList()
            };

            return JsonSerializer.Serialize(pruned);
        }

        [KernelFunction("filter_hotels")]
        [Description("Filter hotels in a city by maximum price per night, minimum rating, or required amenities like pool, wifi, breakfast")]
        public async Task<string> FilterHotelsAsync(
            [Description("City name")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Maximum price per night in USD")] decimal maxPrice = 0,
            [Description("Minimum price per night in USD")] decimal minPrice = 0,
            [Description("Minimum rating from 1 to 5")] double minRating = 0,
            [Description("Required amenities e.g. pool, wifi, breakfast")] List<string>? amenities = null)
        {
            var maxP = maxPrice > 0 ? (decimal?)maxPrice : null;
            var minP = minPrice > 0 ? (decimal?)minPrice : null;
            var minR = minRating > 0 ? (double?)minRating : null;

            var hotels = await _hotelApiService.FilterHotelsAsync(city, checkIn, checkOut, maxP, minP, minR, amenities);

            if (hotels.Count == 0)
                return JsonSerializer.Serialize(new { message = "No hotels matched these filters." });

            return SerializePrunedHotels(hotels);
        }

        [KernelFunction("get_hotels_near_location")]
        [Description("Find hotels near specific coordinates (e.g. a landmark) within a city, sorted by distance")]
        public async Task<string> GetHotelsNearLocationAsync(
            [Description("City name (used for the underlying search)")] string city,
            [Description("Check-in date yyyy-MM-dd")] string checkIn,
            [Description("Check-out date yyyy-MM-dd")] string checkOut,
            [Description("Latitude of the target location")] double latitude,
            [Description("Longitude of the target location")] double longitude,
            [Description("Search radius in kilometers")] int radiusKm = 5)
        {
            var hotels = await _hotelApiService.GetHotelsNearLocationAsync(city, checkIn, checkOut, latitude, longitude, radiusKm);

            if (hotels.Count == 0)
                return JsonSerializer.Serialize(new { message = "No hotels found within this radius." });

            return SerializePrunedHotels(hotels);
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

            return SerializePrunedHotels(hotels);
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

        private string SerializePrunedHotels(List<SmartTravelPlaners.BLL.Features.Hotel.DTOs.GoogleHotelDto> hotels, int maxCount = 5)
        {
            var pruned = hotels.Take(maxCount).Select(h => new
            {
                hotel_id = h.HotelId,
                name = h.Name,
                price = new { price_per_night = h.Price?.PricePerNight },
                rating = new { value = h.Rating?.Value },
                stars = h.Stars,
                location = new { address = h.Location?.Address, latitude = h.Location?.Latitude, longitude = h.Location?.Longitude },
                description = h.Description?.Length > 80 ? h.Description.Substring(0, 80) + "..." : h.Description,
                amenities = h.Amenities?.Take(3).ToList(),
                images = h.Images?.Take(3).ToList()
            }).ToList();

            return JsonSerializer.Serialize(pruned);
        }
    }
}
