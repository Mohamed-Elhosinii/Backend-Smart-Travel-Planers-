using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HotelsController : ControllerBase
    {
        private readonly IHotelApiService _hotelApiService;

        public HotelsController(IHotelApiService hotelApiService)
        {
            _hotelApiService = hotelApiService;
        }

        [HttpGet("hotels")]
        public async Task<IActionResult> GetHotels(
            [FromQuery] string location,
            [FromQuery] string checkIn,
            [FromQuery] string checkOut,
            [FromQuery] int adults = 2,
            [FromQuery] int children = 0)
        {
            if (string.IsNullOrEmpty(location))
            {
                return BadRequest("Location name is required.");
            }
            if (string.IsNullOrEmpty(checkIn) || string.IsNullOrEmpty(checkOut))
            {
                return BadRequest("Check-in and Check-out dates are required (Format: YYYY-MM-DD).");
            }

            var hotels = await _hotelApiService.GetAvailableHotelsAsync(location, checkIn, checkOut, adults, children);

            if (hotels == null || !hotels.Any())
            {
                return NotFound(new
                {
                    message = "No available hotels found on Google Hotels for the specified criteria.",
                    searchedLocation = location,
                    period = $"From {checkIn} To {checkOut}"
                });
            }

            return Ok(hotels);
        }
    }
}