using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.ExternalApis.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestApisController : ControllerBase
    {
        private readonly IPlacesApiService _placesService;

        public TestApisController(IPlacesApiService placesService)
        {
            _placesService = placesService;
        }

        // GET /api/test/places?city=Istanbul&query=restaurants
        [HttpGet("places")]
        public async Task<IActionResult> GetPlaces([FromQuery] string city = "Istanbul",[FromQuery] string? query = null)
        {
            var places = await _placesService.SearchAsync(city, query);
            return Ok(places);
        }


        [HttpGet("{fsqPlaceId}")]
        public async Task<IActionResult> GetDetails(string fsqPlaceId)
        {
            var result =await _placesService.GetPlaceDetailsAsync(fsqPlaceId);

            if (result == null)
                return NotFound();

            return Ok(result);
        }

   
        [HttpGet("{fsqPlaceId}/photos")]
        public async Task<IActionResult> GetPhotos( string fsqPlaceId)
        {
            var result = await _placesService.GetPlacePhotosAsync(fsqPlaceId);

            return Ok(result);
        }

    
        [HttpGet("{fsqPlaceId}/tips")]
        public async Task<IActionResult> GetTips( string fsqPlaceId)
        {
            var result = await _placesService.GetPlaceTipsAsync(fsqPlaceId);

            return Ok(result);
        }

      
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng)
        {
            var result = await _placesService.GetNearbyPlacesAsync(lat, lng);

            return Ok(result);
        }
    }
}