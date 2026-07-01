using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HotelsController : ControllerBase
    {
        private readonly IHotelSearchService _hotelSearchService;
        private readonly IBookingLinksService _bookingLinksService;

        public HotelsController(IHotelSearchService hotelSearchService, IBookingLinksService bookingLinksService)
        {
            _hotelSearchService = hotelSearchService;
            _bookingLinksService = bookingLinksService;
        }

        public class HotelSearchRequest
        {
            public string DestId { get; set; } = string.Empty;
            public string DestType { get; set; } = string.Empty;
            public DateTime CheckIn { get; set; }
            public DateTime CheckOut { get; set; }
            public int Adults { get; set; } = 2;
            public int Rooms { get; set; } = 1;
        }

        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] HotelSearchRequest request)
        {
            if (string.IsNullOrEmpty(request.DestId))
                return BadRequest("DestId is required.");
            if (request.CheckIn == default || request.CheckOut == default)
                return BadRequest("Check-in and Check-out dates are required.");

            var result = await _hotelSearchService.SearchAsync(
                request.DestId, request.DestType, request.CheckIn, request.CheckOut, request.Adults, request.Rooms);

            if (result.Hotels == null || result.Hotels.Count == 0)
                return NotFound("No hotels found for the given criteria.");

            return Ok(result);
        }

        [HttpGet("{hotelName}/booking-links")]
        public async Task<IActionResult> GetBookingLinks(string hotelName, [FromQuery] string? location)
        {
            if (string.IsNullOrEmpty(hotelName))
                return BadRequest("Hotel name is required.");

            var result = await _bookingLinksService.GetLinksAsync(hotelName, location);
            return Ok(result);
        }
    }
}