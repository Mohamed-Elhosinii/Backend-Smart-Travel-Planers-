using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SmartTravelPlaners.BLL.Features.Weather.DTOs;
using SmartTravelPlaners.BLL.Features.Weather.Services;
using SmartTravelPlaners.BLL.Features.Weather.Settings;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Weather
{
    public class WeatherApiServiceTests
    {
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly WeatherApiService _service;

        public WeatherApiServiceTests()
        {
            _handlerMock = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_handlerMock.Object)
            {
                BaseAddress = new Uri("https://weather.test/")
            };

            var options = Options.Create(new WeatherApiSettings
            {
                BaseUrl = "https://weather.test/",
                ApiKey = "test-key"
            });

            _service = new WeatherApiService(_httpClient, options);
        }

        // ============================================================
        // Success Case
        // ============================================================

        [Fact]
        public async Task GetWeatherForTripAsync_ShouldReturnData_WhenApiSuccess()
        {
            var fakeResponse = new VisualCrossingResponseDto
            {
                Address = "Cairo"
            };

            SetupResponse(HttpStatusCode.OK, fakeResponse);

            var result = await _service.GetWeatherForTripAsync(
                "Cairo",
                DateTime.Today,
                DateTime.Today.AddDays(2));

            var weather = Assert.IsType<VisualCrossingResponseDto>(result);

            Assert.NotNull(weather);
            Assert.Equal("Cairo", weather.Address);
        }

        // ============================================================
        // Null Response Case
        // ============================================================

        [Fact]
        public async Task GetWeatherForTripAsync_ShouldReturnEmptyObject_WhenApiReturnsNull()
        {
            SetupResponse(HttpStatusCode.OK, null);

            var result = await _service.GetWeatherForTripAsync(
                "Cairo",
                DateTime.Today,
                DateTime.Today.AddDays(2));

            var weather = Assert.IsType<VisualCrossingResponseDto>(result);

            Assert.NotNull(weather);
        }

        // ============================================================
        // Exception Case
        // ============================================================

        [Fact]
        public async Task GetWeatherForTripAsync_ShouldReturnFallback_WhenExceptionOccurs()
        {
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new Exception("API failed"));

            var result = await _service.GetWeatherForTripAsync(
                "Cairo",
                DateTime.Today,
                DateTime.Today.AddDays(2));

            var weather = Assert.IsType<VisualCrossingResponseDto>(result);

            Assert.NotNull(weather);
            Assert.Equal("Cairo", weather.Address); // fallback
        }

        // ============================================================
        // Helper
        // ============================================================

        private void SetupResponse(HttpStatusCode status, object? content)
        {
            _handlerMock
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
                        : new StringContent(
                            JsonSerializer.Serialize(content),
                            Encoding.UTF8,
                            "application/json")
                });
        }
    }
}