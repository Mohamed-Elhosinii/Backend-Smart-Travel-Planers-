using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;

namespace SmartTravelPlaners.BLL.Features.Hotel.Interfaces
{
    public interface IHotelSearchService
    {
        Task<HotelSearchResponseDto> SearchAsync(string destId, string destType, DateTime checkIn, DateTime checkOut, int adults = 2, int rooms = 1);
    }

    public interface IBookingLinksService
    {
        Task<BookingLinksDto> GetLinksAsync(string hotelName, string? location = null);
    }
}
