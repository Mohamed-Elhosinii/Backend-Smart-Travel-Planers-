using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class PlacesControllerTests
    {
        private readonly Mock<IPlacesApiService> _serviceMock;
        private readonly PlacesController _controller;

        public PlacesControllerTests()
        {
            _serviceMock = new Mock<IPlacesApiService>();
            _controller = new PlacesController(_serviceMock.Object);
        }

        // ============================================================
        // GetPlaces
        // ============================================================

        [Fact]
        public async Task GetPlaces_ShouldReturn200_WithResults()
        {
            _serviceMock.Setup(s => s.SearchAsync("cairo", null, It.IsAny<int>()))
                .ReturnsAsync(new List<PlaceDto>
                {
                    new() { Name = "Cairo Tower", FsqPlaceId = "1" }
                });

            var result = await _controller.GetPlaces("cairo", null) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task GetPlaces_ShouldReturn200_WhenEmpty()
        {
            _serviceMock.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<PlaceDto>());

            var result = await _controller.GetPlaces("cairo", null) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // GetPlacePhotos
        // ============================================================

        [Fact]
        public async Task GetPlacePhotos_ShouldReturn200_WithPhotos()
        {
            _serviceMock.Setup(s => s.GetImages("Cairo Tower", "attraction", "Cairo"))
                .ReturnsAsync(new List<PlacePhotoDto>
                {
            new() { Urls = new List<string> { "https://image.com/photo.jpg" } }
                });

            var result = await _controller.GetPlacePhotos("Cairo Tower", "attraction", "Cairo") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // GetDetails
        // ============================================================

        [Fact]
        public async Task GetDetails_ShouldReturn200_WhenFound()
        {
            _serviceMock.Setup(s => s.GetPlaceDetailsAsync("fsq123"))
                .ReturnsAsync(new PlaceDetailsDto { Name = "Cairo Tower" });

            var result = await _controller.GetDetails("fsq123") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task GetDetails_ShouldReturn404_WhenNotFound()
        {
            _serviceMock.Setup(s => s.GetPlaceDetailsAsync("notexist"))
                .ReturnsAsync((PlaceDetailsDto?)null);

            var result = await _controller.GetDetails("notexist") as NotFoundResult;

            Assert.NotNull(result);
            Assert.Equal(404, result!.StatusCode);
        }

        // ============================================================
        // GetNearby
        // ============================================================

        [Fact]
        public async Task GetNearby_ShouldReturn200_WithResults()
        {
            _serviceMock.Setup(s => s.GetNearbyPlacesAsync(30.0, 31.0))
                .ReturnsAsync(new List<NearbyPlaceDto>
                {
                    new() { Name = "Nearby Place" }
                });

            var result = await _controller.GetNearby(30.0, 31.0) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task GetNearby_ShouldReturn200_WhenEmpty()
        {
            _serviceMock.Setup(s => s.GetNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new List<NearbyPlaceDto>());

            var result = await _controller.GetNearby(0, 0) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }
    }
}