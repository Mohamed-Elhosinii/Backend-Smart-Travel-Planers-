using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Services;
using SmartTravelPlaners.BLL.Features.Hotel.Settings;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Hotel
{
    public class HotelApiServiceTests
    {
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly HotelApiService _service;

        public HotelApiServiceTests()
        {
            _handlerMock = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_handlerMock.Object)
            {
                BaseAddress = new Uri("https://hotel.test")
            };

            var options = Options.Create(new HotelApiSettings
            {
                BaseUrl = "https://hotel.test",
                ApiKey = "test-key"
            });

            var logger = new Mock<ILogger<HotelApiService>>();

            _service = new HotelApiService(
                _httpClient,
                options,
                logger.Object
            );
        }

        // =========================
        // GetAvailableHotelsAsync
        // =========================

        [Fact]
        public async Task GetAvailableHotelsAsync_ShouldReturnHotels_WhenApiSuccess()
        {
            var responseObj = new
            {
                hotels = new[]
                {
                    new
                    {
                        hotel_id = "1",
                        name = "Hilton Cairo",
                        location = new
                        {
                            address = "Cairo",
                            latitude = 30.0,
                            longitude = 31.0
                        },
                        price = new { price_per_night = 100 },
                        rating = new { value = 4.5 },
                        amenities = new[] { "wifi", "pool" }
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetAvailableHotelsAsync(
                "Cairo",
                "2026-07-01",
                "2026-07-05"
            );

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAvailableHotelsAsync_ShouldReturnEmpty_WhenApiFails()
        {
            SetupHttpResponse(HttpStatusCode.BadRequest, null);

            var result = await _service.GetAvailableHotelsAsync(
                "Cairo",
                 "2026-07-01",
                "2026-07-05"
            );

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // =========================
        // FilterHotelsAsync
        // =========================

        [Fact]
        public async Task FilterHotelsAsync_ShouldFilterByPriceAndRating()
        {
            var responseObj = new
            {
                hotels = new[]
                {
                    new
                    {
                        name = "Cheap Hotel",
                        price = new { price_per_night = 50 },
                        rating = new { value = 3.0 },
                        location = new { latitude = 30.0, longitude = 31.0 },
                        amenities = new[] { "wifi" }
                    },
                    new
                    {
                        name = "Luxury Hotel",
                        price = new { price_per_night = 120 },
                        rating = new { value = 5.0 },
                        location = new { latitude = 30.0, longitude = 31.0 },
                        amenities = new[] { "wifi", "pool" }
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.FilterHotelsAsync(
                "Cairo",
                 "2026-07-01",
                "2026-07-05",
                maxPrice: 150,
                minRating: 4,
                 amenities: null
            );

            Assert.NotNull(result);
            Assert.Single(result);
        }

        // =========================
        // GetHotelByIdAsync
        // =========================

        [Fact]
        public async Task GetHotelByIdAsync_ShouldReturnHotel_WhenFound()
        {
            var responseObj = new
            {
                hotels = new[]
                {
                    new
                    {
                        hotel_id = "123",
                        name = "Hilton",
                        location = new { latitude = 30.0, longitude = 31.0 },
                        price = new { price_per_night = 100 },
                        rating = new { value = 4.5 },
                        amenities = new[] { "wifi" }
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetHotelByIdAsync(
                "Cairo",
                "2026-07-01",
                "2026-07-05",
                "123"
            );

            Assert.NotNull(result);
            Assert.Equal("Hilton", result!.Name);
        }
        // =========================
        // GetHotelsNearLocationAsync
        // =========================
        [Fact]
        public async Task GetHotelsNearLocationAsync_ShouldReturnNearbyHotels()
        {
            var responseObj = new
            {
                hotels = new[]
                {
            new
            {
                name = "Near Hotel",
                location = new { latitude = 30.0, longitude = 31.0 },
                price = new { pricePerNight = 100 },
                rating = new { value = 4.5 },
                amenities = new string[] {}
            },
            new
            {
                name = "Far Hotel",
                location = new { latitude = 50.0, longitude = 50.0 },
                price = new { pricePerNight = 100 },
                rating = new { value = 4.5 },
                amenities = new string[] {}
            }
        }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetHotelsNearLocationAsync(
                "Cairo",
                "2025-01-01",
                "2025-01-05",
                30.0,
                31.0,
                radiusKm: 100
            );

            Assert.Single(result); 
            Assert.Equal("Near Hotel", result[0].Name);
        }
        // =========================
        //GetSimilarHotelsAsync
        // =========================
        [Fact]
        public async Task GetSimilarHotelsAsync_ShouldReturnSimilarHotels()
        {
            var responseObj = new
            {
                hotels = new[]
                {
            new
            {
                hotel_id = "1",
                name = "Target Hotel",
                price = new { price_per_night = 100 },
                rating = new { value = 4.0 },
                location = new { latitude = 30.0, longitude = 31.0 },
                amenities = new string[] {}
            },
            new
            {
                hotel_id = "2",
                name = "Similar Hotel",
                price = new { price_per_night = 110 },
                rating = new { value = 4.2 },
                location = new { latitude = 30.0, longitude = 31.0 },
                amenities = new string[] {}
            },
            new
            {
                hotel_id = "3",
                name = "Different Hotel",
                price = new { price_per_night = 300 },
                rating = new { value = 2.0 },
                location = new { latitude = 30.0, longitude = 31.0 },
                amenities = new string[] {}
            }
        }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetSimilarHotelsAsync(
                "Cairo",
                "2025-01-01",
                "2025-01-05",
                "1"
            );

            Assert.Single(result);
            Assert.Equal("Similar Hotel", result[0].Name);
        }

        // =========================
        // Helpers
        // =========================

        private void SetupHttpResponse(HttpStatusCode status, object? content)
        {
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = content == null
                        ? null
                        : new StringContent(
                            JsonSerializer.Serialize(content),
                            Encoding.UTF8,
                            "application/json"
                        )
                });
        }
    }
}
