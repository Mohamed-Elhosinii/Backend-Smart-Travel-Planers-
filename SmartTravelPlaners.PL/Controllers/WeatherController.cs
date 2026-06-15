//using System;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
//using SmartTravelPlaners.BLL.Features.Weather.Interfaces;

//namespace SmartTravelPlaners.PL.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class WeatherController : ControllerBase
//    {
//        private readonly IWeatherService _weatherService;

//        public WeatherController(IWeatherService weatherService)
//        {
//            _weatherService = weatherService;
//        }

//        [HttpGet("trip-weather")]
//        //public async Task<IActionResult> GetWeather([FromQuery] string city, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
//        //{
//        //    try
//        //    {
//        //        //var result = await _weatherService.GetWeatherForTripAsync(city, startDate, endDate);
//        //        //return Ok(result);
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        return StatusCode(500, $"Internal server error: {ex.Message}");
//        //    }
//        //}
//    //}
//}