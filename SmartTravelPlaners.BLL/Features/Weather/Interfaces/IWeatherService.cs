using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SmartTravelPlaners.BLL.Features.Weather.Interfaces
{
    public interface IWeatherApiService
    {
        Task<object> GetWeatherForTripAsync(string cityName, DateTime startDate, DateTime endDate);
    }
}