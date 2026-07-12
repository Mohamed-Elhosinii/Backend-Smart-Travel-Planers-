using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Services;
using SmartTravelPlaners.BLL.Features.Place.Settings;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Place
{
    public class PlacesApiServiceTests
    {
        private readonly Mock<IHttpClientFactory> _factoryMock;
        private readonly Mock<HttpMessageHandler> _foursquareHandlerMock;
        private readonly Mock<HttpMessageHandler> _serperHandlerMock;
        private readonly Mock<ILogger<PlacesApiService>> _loggerMock;

        private readonly HttpClient _foursquareClient;
        private readonly HttpClient _serperClient;

        private readonly PlacesApiService _service;

        public PlacesApiServiceTests()
        {
            _factoryMock = new Mock<IHttpClientFactory>();
            _foursquareHandlerMock = new Mock<HttpMessageHandler>();
            _serperHandlerMock = new Mock<HttpMessageHandler>();

            _foursquareClient = new HttpClient(_foursquareHandlerMock.Object)
            {
                BaseAddress = new Uri("https://foursquare.test")
            };

            _serperClient = new HttpClient(_serperHandlerMock.Object)
            {
                BaseAddress = new Uri("https://serper.test")
            };

            _factoryMock.Setup(f => f.CreateClient("Foursquare"))
                .Returns(_foursquareClient);

            _factoryMock.Setup(f => f.CreateClient("Serper"))
                .Returns(_serperClient);
            _loggerMock = new Mock<ILogger<PlacesApiService>>();

            var fsOptions = Options.Create(new FoursquareSettings
            {
                ServiceKey = "test-key",
                PlacesVersion = "v1"
            });

            var serperOptions = Options.Create(new SerperSettings
            {
                ApiKey = "serper-key"
            });

            _service = new PlacesApiService(
                _factoryMock.Object,
                fsOptions,
                serperOptions,
                _loggerMock.Object);
        }

        // ============================================================
        // SearchAsync
        // ============================================================

        [Fact]
        public async Task SearchAsync_ShouldReturnPlaces_WhenApiReturnsData()
        {
            var responseObj = new
            {
                results = new[]
                {
                    new
                    {
                        fsq_place_id = "1",
                        name = "Eiffel Tower",
                        location = new { address = "Paris" }
                    }
                }
            };

            SetupFoursquareResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.SearchAsync("Paris", "tower");

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Eiffel Tower", result[0].Name);
        }

        [Fact]
        public async Task SearchAsync_ShouldReturnEmpty_WhenApiReturnsNull()
        {
            SetupFoursquareResponse(HttpStatusCode.OK, null);

            var result = await _service.SearchAsync("Paris");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ============================================================
        // GetPlaceDetailsAsync
        // ============================================================

        [Fact]
        public async Task GetPlaceDetailsAsync_ShouldReturnDetails_WhenApiSuccess()
        {
            var responseObj = new
            {
                id = "1",
                name = "Eiffel Tower"
            };

            SetupFoursquareResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetPlaceDetailsAsync("1");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetPlaceDetailsAsync_ShouldThrow_WhenApiFails()
        {
            SetupFoursquareResponse(HttpStatusCode.BadRequest, null);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetPlaceDetailsAsync("1"));
        }

        // ============================================================
        // GetNearbyPlacesAsync
        // ============================================================

        [Fact]
        public async Task GetNearbyPlacesAsync_ShouldReturnList_WhenApiSuccess()
        {
            var responseObj = new
            {
                candidates = new[]
                {
                    new { name = "Place 1", lat = 1.1, lng = 2.2 }
                }
            };

            SetupFoursquareResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetNearbyPlacesAsync(30.0, 31.0);

            Assert.NotNull(result);
            Assert.Single(result);
        }

        // ============================================================
        // GetImages
        // ============================================================

        [Fact]
        public async Task GetImages_ShouldReturnImages_WhenSerperReturnsValidData()
        {
            var responseObj = new
            {
                images = new[]
                {
                    new { imageUrl = "https://test.com/a.jpg" },
                    new { imageUrl = "https://test.com/b.png" }
                }
            };

            SetupSerperResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetImages("Eiffel", "tower", null);

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.True(result.All(x => x.Urls.Count > 0));
        }

        [Fact]
        public async Task GetImages_ShouldReturnEmpty_WhenApiFails()
        {
            SetupSerperResponse(HttpStatusCode.BadRequest, null);

            var result = await _service.GetImages("Eiffel", "tower", null);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetImages_ShouldFilterInvalidUrls()
        {
            var responseObj = new
            {
                images = new[]
                {
                    new { imageUrl = "https://instagram.com/x" },
                    new { imageUrl = "https://test.com/a.jpg" }
                }
            };

            SetupSerperResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetImages("Eiffel", "tower", null);

            Assert.Single(result);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void SetupFoursquareResponse(HttpStatusCode status, object? content)
        {
            _foursquareHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = content == null
                        ? null
                        : new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
                });
        }

        private void SetupSerperResponse(HttpStatusCode status, object? content)
        {
            _serperHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = content == null
                        ? null
                        : new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
                });
        }
    }
}