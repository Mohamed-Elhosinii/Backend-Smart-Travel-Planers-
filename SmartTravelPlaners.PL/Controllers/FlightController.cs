using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/flights")]
    public class FlightController : ControllerBase
    {
        private readonly IFlightService _flightService;

        public FlightController(IFlightService flightService)
        {
            _flightService = flightService;
        }

        // Search for one-way or round-trip flights by city name
        [HttpGet("search")]
        public async Task<IActionResult> SearchFlights(
            [FromQuery] string departure = "Cairo",
            [FromQuery] string arrival = "Dubai",
            [FromQuery] string departureDate = "2026-06-20",
            [FromQuery] TripType tripType = TripType.OneWay,
            [FromQuery] string? returnDate = null)
        {
            try
            {
                if (tripType == TripType.RoundTrip && string.IsNullOrWhiteSpace(returnDate))
                    return BadRequest(new { error = "returnDate is required for RoundTrip" });

                var request = new FlightSearchRequest
                {
                    DepartureCity = departure,
                    ArrivalCity = arrival,
                    DepartureDate = departureDate,
                    TripType = tripType,
                    ReturnDate = returnDate
                };

                var result = await _flightService.SearchFlightsAsync(request);

                return Ok(new
                {
                    tripType = tripType.ToString(),
                    isRoundTrip = result.IsRoundTrip,
                    departureIata = result.DepartureIata,
                    arrivalIata = result.ArrivalIata,
                    outboundCount = result.OutboundFlights.Count,
                    returnCount = result.ReturnFlights?.Count ?? 0,
                    outboundFlights = result.OutboundFlights,
                    returnFlights = result.ReturnFlights
                });
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