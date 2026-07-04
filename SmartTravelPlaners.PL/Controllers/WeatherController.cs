using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Weather.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/weather")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherApiService _weatherService;
        private readonly ILogger<WeatherController> _logger;

        public WeatherController(IWeatherApiService weatherService, ILogger<WeatherController> logger)
        {
            _weatherService = weatherService;
            _logger = logger;
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
            {
                _logger.LogWarning("Trip weather retrieval attempted with missing city");
                return BadRequest(new { error = "city is required" });
            }

            if (!DateTime.TryParse(startDate, out var start) ||
                !DateTime.TryParse(endDate, out var end))
            {
                _logger.LogWarning("Trip weather retrieval attempted with invalid dates. StartDate: {StartDate}, EndDate: {EndDate}", startDate, endDate);
                return BadRequest(new { error = "startDate and endDate must be valid dates (Format: yyyy-MM-dd)" });
            }

            if (end < start)
            {
                _logger.LogWarning("Trip weather retrieval attempted with endDate before startDate. StartDate: {StartDate}, EndDate: {EndDate}", startDate, endDate);
                return BadRequest(new { error = "endDate must be on or after startDate" });
            }

            try
            {
                _logger.LogInformation("Trip weather retrieval initiated. City: {City}, StartDate: {StartDate}, EndDate: {EndDate}", city, startDate, endDate);
                var result = await _weatherService.GetWeatherForTripAsync(city, start, end);
                _logger.LogInformation("Trip weather retrieved successfully for city: {City}", city);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trip weather retrieval failed for city: {City}. Error: {ErrorMessage}", city, ex.Message);
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }
    }
}
