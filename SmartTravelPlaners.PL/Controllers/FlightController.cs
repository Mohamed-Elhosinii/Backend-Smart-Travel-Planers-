using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/flights")]
    public class FlightController : ControllerBase
    {
        private readonly IFlightService _flightService;
        private readonly ILogger<FlightController> _logger;

        public FlightController(IFlightService flightService, ILogger<FlightController> logger)
        {
            _flightService = flightService;
            _logger = logger;
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
                {
                    _logger.LogWarning("Flight search attempted with RoundTrip but no return date. Departure: {Departure}, Arrival: {Arrival}", departure, arrival);
                    return BadRequest(new { error = "returnDate is required for RoundTrip" });
                }

                _logger.LogInformation("Flight search initiated. Departure: {Departure}, Arrival: {Arrival}, DepartureDate: {DepartureDate}, TripType: {TripType}", departure, arrival, departureDate, tripType);

                var request = new FlightSearchRequest
                {
                    DepartureCity = departure,
                    ArrivalCity = arrival,
                    DepartureDate = departureDate,
                    TripType = tripType,
                    ReturnDate = returnDate
                };

                var result = await _flightService.SearchFlightsAsync(request);

                _logger.LogInformation("Flight search completed successfully. OutboundFlights: {OutboundCount}, ReturnFlights: {ReturnCount}", result.OutboundFlights.Count, result.ReturnFlights?.Count ?? 0);

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
                _logger.LogError(ex, "Flight search failed. Departure: {Departure}, Arrival: {Arrival}, Error: {ErrorMessage}", departure, arrival, ex.Message);
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }
    }
}