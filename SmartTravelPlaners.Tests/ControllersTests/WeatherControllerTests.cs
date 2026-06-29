using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Weather.DTOs;
using SmartTravelPlaners.BLL.Features.Weather.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class WeatherControllerTests
    {
        private readonly Mock<IWeatherApiService> _serviceMock;
        private readonly WeatherController _controller;

        public WeatherControllerTests()
        {
            _serviceMock = new Mock<IWeatherApiService>();
            _controller = new WeatherController(_serviceMock.Object);
        }

        // ============================================================
        // 400 validations
        // ============================================================

        [Fact]
        public async Task GetTripWeather_ShouldReturn400_WhenCityEmpty()
        {
            var result = await _controller.GetTripWeather("", "2026-06-20", "2026-06-22") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        [Fact]
        public async Task GetTripWeather_ShouldReturn400_WhenStartDateInvalid()
        {
            var result = await _controller.GetTripWeather("Cairo", "not-a-date", "2026-06-22") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        [Fact]
        public async Task GetTripWeather_ShouldReturn400_WhenEndDateBeforeStartDate()
        {
            var result = await _controller.GetTripWeather("Cairo", "2026-06-22", "2026-06-20") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // 200
        // ============================================================

        [Fact]
        public async Task GetTripWeather_ShouldReturn200_WhenSuccess()
        {
            _serviceMock.Setup(s => s.GetWeatherForTripAsync("Cairo",
                new DateTime(2026, 6, 20), new DateTime(2026, 6, 22)))
                .ReturnsAsync(new VisualCrossingResponseDto
                {
                    Address = "Cairo",
                    Days = new List<VisualCrossingDayItem>
                    {
                        new() { Datetime = "2026-06-20", TempMax = 35, TempMin = 25 }
                    }
                });

            var result = await _controller.GetTripWeather("Cairo", "2026-06-20", "2026-06-22") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // 500
        // ============================================================

        [Fact]
        public async Task GetTripWeather_ShouldReturn500_WhenServiceThrows()
        {
            _serviceMock.Setup(s => s.GetWeatherForTripAsync(It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("API Error"));

            var result = await _controller.GetTripWeather("Cairo", "2026-06-20", "2026-06-22") as ObjectResult;

            Assert.NotNull(result);
            Assert.Equal(500, result!.StatusCode);
        }
    }
}