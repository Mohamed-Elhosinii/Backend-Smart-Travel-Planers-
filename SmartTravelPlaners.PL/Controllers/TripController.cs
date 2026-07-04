using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<TripController> _logger;

        public TripController(
            ITripCreationService tripCreationService,
            ITripRepository tripRepository,
            IPlacesApiService placesService,
            ILogger<TripController> logger)
        {
            _tripCreationService = tripCreationService;
            _tripRepository = tripRepository;
            _placesService = placesService;
            _logger = logger;
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
            {
                _logger.LogWarning("Unauthorized trip quick plan creation attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Quick plan trip creation initiated for UserId: {UserId}, Destination: {Destination}", userId, dto.Destination);
                var result = await _tripCreationService.CreateAndBuildAsync(dto, userId);

                if (result.LimitReached)
                {
                    _logger.LogWarning("Trip creation limit reached for UserId: {UserId}", userId);
                    return Ok(new { message = result.Message });
                }

                _logger.LogInformation("Quick plan trip created successfully for UserId: {UserId}, TripId: {TripId}", userId, result.TripId);
                return Ok(new { tripId = result.TripId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick plan trip creation failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
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
            {
                _logger.LogWarning("Unauthorized user trips retrieval attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("User trips retrieval requested for UserId: {UserId}", userId);
                var profile = await userProfileRepo.GetUserProfileWithPreferencesAsync(userId);
                if (profile == null)
                {
                    _logger.LogWarning("User profile not found for UserId: {UserId}", userId);
                    return Unauthorized();
                }

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

                _logger.LogInformation("User trips retrieved successfully for UserId: {UserId}. TripsCount: {TripsCount}", userId, summaries.Count);
                return Ok(summaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User trips retrieval failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        // GET api/trip/{tripId}/suggestions
        [HttpGet("{tripId}/suggestions")]
        public async Task<IActionResult> GetSuggestions(Guid tripId, [FromQuery] int limit = 6)
        {
            try
            {
                _logger.LogInformation("Trip suggestions retrieval requested for TripId: {TripId}, Limit: {Limit}", tripId, limit);
                var trip = await _tripRepository.GetByIdAsync(tripId);
                if (trip == null)
                {
                    _logger.LogWarning("Trip not found for TripId: {TripId}", tripId);
                    return NotFound("Trip not found.");
                }

                var suggestions = await _placesService.SearchAsync(
                    trip.Destination,
                    query: null,
                    limit: limit);

                _logger.LogInformation("Trip suggestions retrieved successfully for TripId: {TripId}. SuggestionsCount: {SuggestionsCount}", tripId, suggestions.Count);
                return Ok(suggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trip suggestions retrieval failed for TripId: {TripId}. Error: {ErrorMessage}", tripId, ex.Message);
                throw;
            }
        }
    }
}