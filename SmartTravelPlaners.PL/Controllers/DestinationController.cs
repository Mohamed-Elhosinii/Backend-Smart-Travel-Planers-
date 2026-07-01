using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/places")]
    public class DestinationController : ControllerBase
    {
        private readonly IPlaceResolverService _resolverService;

        public DestinationController(IPlaceResolverService resolverService)
        {
            _resolverService = resolverService;
        }

        public class ResolveRequest
        {
            public string Query { get; set; } = string.Empty;
        }

        [HttpPost("resolve")]
        public async Task<IActionResult> Resolve([FromBody] ResolveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return BadRequest("Query is required.");

            var result = await _resolverService.ResolveAsync(request.Query);
            return Ok(result);
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
                return BadRequest("DestId and ResolvedName are required.");

            var result = await _resolverService.ConfirmAsync(request.DestId, request.ResolvedName);
            return Ok(result);
        }
    }
}
