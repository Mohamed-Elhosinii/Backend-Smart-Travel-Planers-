using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class HotelsControllerTests
    {
        private readonly Mock<IHotelApiService> _serviceMock;
        private readonly HotelsController _controller;

        public HotelsControllerTests()
        {
            _serviceMock = new Mock<IHotelApiService>();
            _controller = new HotelsController(_serviceMock.Object);
        }

        private List<GoogleHotelDto> MakeHotels() => new List<GoogleHotelDto>
        {
            new()
            {
                Name = "Hilton Cairo",
                Location = new() { Address = "Cairo, Egypt", Latitude = 30.0, Longitude = 31.0 },
                Price = new() { PricePerNight = 150.0 },
                Rating = new() { Value = 4.5 },
                Images = new()
            }
        };

        // ============================================================
        // GetHotels — 400 validations
        // ============================================================

        [Fact]
        public async Task GetHotels_ShouldReturn400_WhenLocationEmpty()
        {
            var result = await _controller.GetHotels("", "2026-07-01", "2026-07-05") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        [Fact]
        public async Task GetHotels_ShouldReturn400_WhenCheckInEmpty()
        {
            var result = await _controller.GetHotels("Cairo", "", "2026-07-05") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        [Fact]
        public async Task GetHotels_ShouldReturn400_WhenCheckOutEmpty()
        {
            var result = await _controller.GetHotels("Cairo", "2026-07-01", "") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // GetHotels — 404
        // ============================================================

        [Fact]
        public async Task GetHotels_ShouldReturn404_WhenNoHotelsFound()
        {
            _serviceMock.Setup(s => s.GetAvailableHotelsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<GoogleHotelDto>());

            var result = await _controller.GetHotels("Cairo", "2026-07-01", "2026-07-05") as NotFoundObjectResult;

            Assert.NotNull(result);
            Assert.Equal(404, result!.StatusCode);
        }

        // ============================================================
        // GetHotels — 200
        // ============================================================

        [Fact]
        public async Task GetHotels_ShouldReturn200_WhenHotelsFound()
        {
            _serviceMock.Setup(s => s.GetAvailableHotelsAsync("Cairo", "2026-07-01", "2026-07-05", 2, 0))
                .ReturnsAsync(MakeHotels());

            var result = await _controller.GetHotels("Cairo", "2026-07-01", "2026-07-05") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }
    }
}