using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/places")]
    public class PlacesController : ControllerBase
    {
        private readonly IPlacesApiService _placesService;
        private readonly ILogger<PlacesController> _logger;

        public PlacesController(IPlacesApiService placesService, ILogger<PlacesController> logger)
        {
            _placesService = placesService;
            _logger = logger;
        }
      
        public class ResolveRequest
        {
            public string Query { get; set; }
        }

        [HttpPost("resolve")]
        public IActionResult ResolveDestination([FromBody] ResolveRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req?.Query))
                    return BadRequest("Query is required");

                _logger.LogInformation("Resolving destination: {Query}", req.Query);
                
                return Ok(new
                {
                    status = 0, // 0 = Resolved
                    destId = Guid.NewGuid().ToString(),
                    destType = "city",
                    resolvedName = req.Query,
                    originalInput = req.Query,
                    source = "fallback",
                    suggestion = (object)null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve destination. Error: {ErrorMessage}", ex.Message);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("places")]
        public async Task<IActionResult> GetPlaces([FromQuery] string city = "cairo",[FromQuery] string? query = null)
        {
            try
            {
                _logger.LogInformation("Places search initiated. City: {City}, Query: {Query}", city, query ?? "Not specified");
                var places = await _placesService.SearchAsync(city, query);
                _logger.LogInformation("Places search completed successfully. PlacesCount: {PlacesCount}", places.Count);
                return Ok(places);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Places search failed for city: {City}. Error: {ErrorMessage}", city, ex.Message);
                throw;
            }
        }

        [HttpGet("photos")]
        public async Task<IActionResult> GetPlacePhotos([FromQuery] string placeName, [FromQuery] string category, [FromQuery] string address)
        {
            try
            {
                _logger.LogInformation("Place photos retrieval initiated. PlaceName: {PlaceName}, Category: {Category}, Address: {Address}", placeName, category, address);
                var result = await _placesService.GetImages(placeName, category, address);
                _logger.LogInformation("Place photos retrieved successfully for place: {PlaceName}", placeName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Place photos retrieval failed. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        [HttpGet("{fsqPlaceId}")]
        public async Task<IActionResult> GetDetails(string fsqPlaceId)
        {
            try
            {
                _logger.LogInformation("Place details retrieval initiated for PlaceId: {PlaceId}", fsqPlaceId);
                var result = await _placesService.GetPlaceDetailsAsync(fsqPlaceId);

                if (result == null)
                {
                    _logger.LogWarning("Place details not found for PlaceId: {PlaceId}", fsqPlaceId);
                    return NotFound();
                }

                _logger.LogInformation("Place details retrieved successfully for PlaceId: {PlaceId}", fsqPlaceId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Place details retrieval failed for PlaceId: {PlaceId}. Error: {ErrorMessage}", fsqPlaceId, ex.Message);
                throw;
            }
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng)
        {
            try
            {
                _logger.LogInformation("Nearby places search initiated. Latitude: {Lat}, Longitude: {Lng}", lat, lng);
                var result = await _placesService.GetNearbyPlacesAsync(lat, lng);
                _logger.LogInformation("Nearby places search completed successfully. PlacesCount: {PlacesCount}", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nearby places search failed for coordinates ({Lat}, {Lng}). Error: {ErrorMessage}", lat, lng, ex.Message);
                throw;
            }
        }
    }
}