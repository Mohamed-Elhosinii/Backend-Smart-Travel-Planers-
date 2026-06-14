using System;
using System.Threading.Tasks;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Interfaces
{
    public interface IWeatherApiService
    {
        Task<object> GetWeatherForTripAsync(string cityName, DateTime startDate, DateTime endDate);
    }
}