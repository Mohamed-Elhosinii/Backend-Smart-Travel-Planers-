using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Weather.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/weather")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherApiService _weatherService;

        public WeatherController(IWeatherApiService weatherService)
        {
            _weatherService = weatherService;
        }

        // GET /api/weather/trip-weather?city=Cairo&startDate=2026-06-20&endDate=2026-06-22
        // Returns the Visual Crossing forecast timeline (the same data the orchestrator uses).
        [HttpGet("trip-weather")]
        public async Task<IActionResult> GetTripWeather(
            [FromQuery] string city = "Cairo",
            [FromQuery] string startDate = "2026-06-20",
            [FromQuery] string endDate = "2026-06-22")
        {
            if (string.IsNullOrWhiteSpace(city))
                return BadRequest(new { error = "city is required" });

            if (!DateTime.TryParse(startDate, out var start) ||
                !DateTime.TryParse(endDate, out var end))
                return BadRequest(new { error = "startDate and endDate must be valid dates (Format: yyyy-MM-dd)" });

            if (end < start)
                return BadRequest(new { error = "endDate must be on or after startDate" });

            try
            {
                var result = await _weatherService.GetWeatherForTripAsync(city, start, end);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }
    }
}
