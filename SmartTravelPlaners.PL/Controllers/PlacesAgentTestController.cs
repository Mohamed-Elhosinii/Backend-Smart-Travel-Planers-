using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Agents.PlacesAgent;

namespace SmartTravelPlaners.PL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlacesAgentTestController : ControllerBase


    {
    
        private readonly PlacesAgent _placesAgent;

        public PlacesAgentTestController(PlacesAgent placesAgent)
        {
            _placesAgent = placesAgent;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            var result = await _placesAgent.RunAsync(request.Message);
            return Ok(result);
        }
    }
}
public class AskRequest
{
    public string Message { get; set; } = string.Empty;
}


