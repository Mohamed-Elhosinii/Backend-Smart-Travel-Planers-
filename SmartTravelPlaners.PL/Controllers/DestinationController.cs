using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/places")]
    public class DestinationController : ControllerBase
    {
        private readonly IPlaceResolverService _resolverService;
        private readonly ILogger<DestinationController> _logger;

        public DestinationController(IPlaceResolverService resolverService, ILogger<DestinationController> logger)
        {
            _resolverService = resolverService;
            _logger = logger;
        }

        public class ResolveRequest
        {
            public string Query { get; set; } = string.Empty;
        }

        [HttpPost("resolve")]
        public async Task<IActionResult> Resolve([FromBody] ResolveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                _logger.LogWarning("Place resolution request received with empty query");
                return BadRequest("Query is required.");
            }

            try
            {
                _logger.LogInformation("Resolving place for query: {Query}", request.Query);
                var result = await _resolverService.ResolveAsync(request.Query);
                _logger.LogInformation("Place resolved successfully for query: {Query}", request.Query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving place for query: {Query}. Error: {ErrorMessage}", request.Query, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        public class ConfirmRequest
        {
            public string DestId { get; set; } = string.Empty;
            public string ResolvedName { get; set; } = string.Empty;
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm([FromBody] ConfirmRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DestId) || string.IsNullOrWhiteSpace(request.ResolvedName))
            {
                _logger.LogWarning("Place confirmation request received with missing parameters");
                return BadRequest("DestId and ResolvedName are required.");
            }

            try
            {
                _logger.LogInformation("Confirming place: {DestId}", request.DestId);
                var result = await _resolverService.ConfirmAsync(request.DestId, request.ResolvedName);
                _logger.LogInformation("Place confirmed successfully: {DestId}", request.DestId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming place: {DestId}. Error: {ErrorMessage}", request.DestId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
