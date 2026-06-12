using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.ExternalApis.FourSquare.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class PlacesController : ControllerBase
    {
        private readonly IPlacesApiService _placesService;

        public PlacesController(IPlacesApiService placesService)
        {
            _placesService = placesService;
        }
      

        [HttpGet("places")]
        public async Task<IActionResult> GetPlaces([FromQuery] double? lon,[FromQuery] string city = "cairo",[FromQuery] string? query = null)
        {
            var places = await _placesService.SearchAsync( city, query);
            return Ok(places);
        }
        [HttpGet("photos")]
        public async Task<IActionResult> GetPlacePhotos([FromQuery] string placeName, [FromQuery] string category, [FromQuery] string address)
        {
            var result = await _placesService.GetImages(placeName, category, address);
            return Ok(result);
        }


        [HttpGet("{fsqPlaceId}")]
        public async Task<IActionResult> GetDetails(string fsqPlaceId)
        {
            var result =await _placesService.GetPlaceDetailsAsync(fsqPlaceId);

            if (result == null)
                return NotFound();

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