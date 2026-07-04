using System.Collections.Generic;
using System.Threading.Tasks;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;

namespace SmartTravelPlaners.BLL.Features.Hotel.Interfaces
{
    public interface IHotelApiService
    {
        Task<List<GoogleHotelDto>> GetAvailableHotelsAsync(
            string location, string checkIn, string checkOut,
            int adults = 2, int children = 0);

        Task<GoogleHotelDto?> GetHotelByIdAsync(
            string location, string checkIn, string checkOut, string hotelId);

        Task<List<GoogleHotelDto>> FilterHotelsAsync(
            string location, string checkIn, string checkOut,
            decimal? maxPrice, double? minRating, List<string>? amenities,
            int adults = 2, int children = 0);

        Task<List<GoogleHotelDto>> GetHotelsNearLocationAsync(
            string location, string checkIn, string checkOut,
            double latitude, double longitude, int radiusKm,
            int adults = 2, int children = 0);

        Task<List<GoogleHotelDto>> GetSimilarHotelsAsync(
            string location, string checkIn, string checkOut,
            string hotelId, int adults = 2, int children = 0);

        Task<bool> CheckAvailabilityAsync(
            string location, string checkIn, string checkOut, string hotelId);
    }
}

