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

        /// <summary>
        /// Form-driven trip creation (no chat / AI). Reuses the exact same pipeline as the
        /// chat's TRIP_READY marker: create a Draft trip, start the background plan build,
        /// increment usage, and return the new tripId immediately.
        /// </summary>
        /// <remarks>
        /// The plan is intentionally NOT returned — it is still building in the background.
        /// The client polls the existing GET /api/Chat/plan/{tripId} until it returns 200.
        /// </remarks>
        [HttpPost("quick-plan")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickPlan([FromBody] TripCreateDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

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

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTrips(
            [FromServices] IUserProfileRepository userProfileRepo)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var profile = await userProfileRepo.GetUserProfileWithPreferencesAsync(userId);
            if (profile == null)
                return Unauthorized();

            var trips = await _tripRepository.GetUserTripsAsync(profile.Id);

            var summaries = trips.Select(t => new SmartTravelPlaners.BLL.Features.Trips.DTOs.TripSummaryDto
            {
                Id = t.Id,
                Destination = t.Destination,
                OriginCity = t.OriginCity,
                StartDate = t.StartDate.ToString("yyyy-MM-dd"),
                EndDate = t.EndDate.ToString("yyyy-MM-dd"),
                BudgetTotal = t.BudgetTotal,
                BudgetSpent = t.BudgetSpent,
                Status = t.Status.ToString(),
                TravelStyle = t.Preferences
                    .Where(p => p.Category.Equals("TravelStyle", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Value)
                    .ToList(),
                CoverImage = null
            }).ToList();

            return Ok(summaries);
        }

        // GET api/trip/{tripId}/suggestions
        [HttpGet("{tripId}/suggestions")]
        public async Task<IActionResult> GetSuggestions(Guid tripId, [FromQuery] int limit = 6)
        {
            var trip = await _tripRepository.GetByIdAsync(tripId);
            if (trip == null)
                return NotFound("Trip not found.");

            var suggestions = await _placesService.SearchAsync(
                trip.Destination,
                query: null,
                limit: limit);

            return Ok(suggestions);
        }
    }
}