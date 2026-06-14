using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherApiService _weatherApiService;

        public WeatherController(IWeatherApiService weatherApiService)
        {
            _weatherApiService = weatherApiService;
        }

        [HttpGet("trip-weather")]
        public async Task<IActionResult> GetWeather([FromQuery] string city, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            try
            {
                var result = await _weatherApiService.GetWeatherForTripAsync(city, startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}