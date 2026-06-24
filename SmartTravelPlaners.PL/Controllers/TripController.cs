using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TripController : ControllerBase
    {
        private readonly ITripCreationService _tripCreationService;

        public TripController(ITripCreationService tripCreationService)
        {
            _tripCreationService = tripCreationService;
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
        /// <response code="200">
        /// Build started: <c>{ "tripId": "&lt;guid&gt;" }</c>.
        /// Or, if the monthly trip limit is reached: <c>{ "message": "..." }</c> (no trip created).
        /// </response>
        /// <response code="400">Validation failed, or the trip could not be created.</response>
        /// <response code="401">Missing/invalid bearer token.</response>
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

                // Mirror the chat's limit response shape (a message, no usable tripId).
                if (result.LimitReached)
                    return Ok(new { message = result.Message });

                // Build started in the background — return the id to poll on.
                return Ok(new { tripId = result.TripId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
