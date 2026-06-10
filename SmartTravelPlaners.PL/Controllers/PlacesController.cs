using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.ExternalApis.Interfaces.Foursquare;

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
        public async Task<IActionResult> GetPlaces([FromQuery] double? lat ,[FromQuery] double? lon,[FromQuery] string city = "cairo",[FromQuery] string? query = null)
        {
            var places = await _placesService.SearchAsync(lat, lon, city, query);
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

   
        //[HttpGet("{fsqPlaceId}/photos")]
        //public async Task<IActionResult> GetPhotos( string fsqPlaceId)
        //{
        //    var result = await _placesService.GetPlacePhotosAsync(fsqPlaceId);

        //    return Ok(result);
        //}

    
        //[HttpGet("{fsqPlaceId}/tips")]
        //public async Task<IActionResult> GetTips( string fsqPlaceId)
        //{
        //    var result = await _placesService.GetPlaceTipsAsync(fsqPlaceId);

        //    return Ok(result);
        //}

      
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng)
        {
            var result = await _placesService.GetNearbyPlacesAsync(lat, lng);

            return Ok(result);
        }
    }
}