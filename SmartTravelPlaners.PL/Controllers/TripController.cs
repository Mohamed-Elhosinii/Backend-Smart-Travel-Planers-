using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TripController : ControllerBase
    {
        private readonly ITripCreationService _tripCreationService;
        private readonly ITripRepository _tripRepository;
        private readonly IPlacesApiService _placesService;

        public TripController(
            ITripCreationService tripCreationService,
            ITripRepository tripRepository,
            IPlacesApiService placesService)
        {
            _tripCreationService = tripCreationService;
            _tripRepository = tripRepository;
            _placesService = placesService;
        }

        [HttpPost("quick-plan")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickPlan([FromBody] TripCreateDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _tripCreationService.CreateAndBuildAsync(dto, userId);

                if (result.LimitReached)
                    return Ok(new { message = result.Message });

                return Ok(new { tripId = result.TripId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET api/trip/{tripId}/suggestions
        [HttpGet("{tripId}/suggestions")]
        public async Task<IActionResult> GetSuggestions(Guid tripId, [FromQuery] int limit = 6)
        {
            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null) return NotFound("Trip not found.");

            var suggestions = await _placesService.SearchAsync(trip.Destination, query: null, limit: limit);
            return Ok(suggestions);
        }
    }
}