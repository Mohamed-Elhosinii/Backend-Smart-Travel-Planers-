using System.Collections.Generic;
using System.Threading.Tasks;
using SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.DTOs;

namespace SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.Interfaces
{
    public interface IHotelApiService
    {
        Task<List<GoogleHotelDto>> GetAvailableHotelsAsync(
            string location,
            string checkIn,
            string checkOut,
            int adults = 2,
            int children = 0);
    }
}

