using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HotelsController : ControllerBase
    {
        private readonly IHotelSearchService _hotelSearchService;
        private readonly IBookingLinksService _bookingLinksService;
        private readonly ILogger<HotelsController> _logger;

        public HotelsController(IHotelSearchService hotelSearchService, IBookingLinksService bookingLinksService, ILogger<HotelsController> logger)
        {
            _hotelSearchService = hotelSearchService;
            _bookingLinksService = bookingLinksService;
            _logger = logger;
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
            {
                _logger.LogWarning("Hotel search attempted with missing DestId");
                return BadRequest("DestId is required.");
            }
            if (request.CheckIn == default || request.CheckOut == default)
            {
                _logger.LogWarning("Hotel search attempted with invalid dates. CheckIn: {CheckIn}, CheckOut: {CheckOut}", request.CheckIn, request.CheckOut);
                return BadRequest("Check-in and Check-out dates are required.");
            }

            try
            {
                _logger.LogInformation("Hotel search initiated. DestId: {DestId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}, Adults: {Adults}, Rooms: {Rooms}", 
                    request.DestId, request.CheckIn.ToString("yyyy-MM-dd"), request.CheckOut.ToString("yyyy-MM-dd"), request.Adults, request.Rooms);

                var result = await _hotelSearchService.SearchAsync(
                    request.DestId, request.DestType, request.CheckIn, request.CheckOut, request.Adults, request.Rooms);

                if (result.Hotels == null || result.Hotels.Count == 0)
                {
                    _logger.LogWarning("No hotels found for search. DestId: {DestId}, CheckIn: {CheckIn}, CheckOut: {CheckOut}", 
                        request.DestId, request.CheckIn.ToString("yyyy-MM-dd"), request.CheckOut.ToString("yyyy-MM-dd"));
                    return NotFound("No hotels found for the given criteria.");
                }

                _logger.LogInformation("Hotel search completed successfully. HotelsCount: {HotelsCount}", result.Hotels.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotel search failed for DestId: {DestId}. Error: {ErrorMessage}", request.DestId, ex.Message);
                throw;
            }
        }

        [HttpGet("{hotelName}/booking-links")]
        public async Task<IActionResult> GetBookingLinks(string hotelName, [FromQuery] string? location)
        {
            if (string.IsNullOrEmpty(hotelName))
            {
                _logger.LogWarning("Booking links retrieval attempted with missing hotel name");
                return BadRequest("Hotel name is required.");
            }

            try
            {
                _logger.LogInformation("Booking links retrieval initiated. HotelName: {HotelName}, Location: {Location}", hotelName, location ?? "Not specified");
                var result = await _bookingLinksService.GetLinksAsync(hotelName, location);
                _logger.LogInformation("Booking links retrieved successfully for hotel: {HotelName}", hotelName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Booking links retrieval failed for hotel: {HotelName}. Error: {ErrorMessage}", hotelName, ex.Message);
                throw;
            }
        }
    }
}