using Moq;
using Moq.Protected;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Flight
{
    public class FlightServiceTests
    {
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly FlightService _service;

        public FlightServiceTests()
        {
            _handlerMock = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_handlerMock.Object);

            _service = new FlightService(_httpClient);
        }

        // ============================================================
        // GetIataCodeAsync
        // ============================================================

        [Fact]
        public async Task GetIataCodeAsync_ShouldReturnIata_WhenApiSuccess()
        {
            var responseObj = new
            {
                response = new
                {
                    airports = new[]
                    {
                        new { iata_code = "CAI" }
                    }
                }
            };

            SetupResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetIataCodeAsync("Cairo");

            Assert.Equal("CAI", result);
        }

        [Fact]
        public async Task GetIataCodeAsync_ShouldThrow_WhenApiFails()
        {
            SetupResponse(HttpStatusCode.BadRequest, null);

            await Assert.ThrowsAsync<Exception>(() =>
                _service.GetIataCodeAsync("Cairo"));
        }

        // ============================================================
        // SearchFlightsAsync (OneWay)
        // ============================================================

        [Fact]
        public async Task SearchFlightsAsync_ShouldReturnFlights_WhenOneWay()
        {
            SetupMultiResponses();

            var request = new FlightSearchRequest
            {
                DepartureCity = "Cairo",
                ArrivalCity = "Paris",
                DepartureDate = "2026-07-01",
                TripType = TripType.OneWay
            };

            var result = await _service.SearchFlightsAsync(request);

            Assert.NotNull(result);
            Assert.NotEmpty(result.OutboundFlights);
            Assert.Equal("CAI", result.DepartureIata);
            Assert.Equal("CDG", result.ArrivalIata);
        }

        // ============================================================
        // SearchFlightsAsync (RoundTrip)
        // ============================================================

        [Fact]
        public async Task SearchFlightsAsync_ShouldReturnReturnFlights_WhenRoundTrip()
        {
            SetupMultiResponses();

            var request = new FlightSearchRequest
            {
                DepartureCity = "Cairo",
                ArrivalCity = "Paris",
                DepartureDate = "2026-07-01",
                ReturnDate = "2026-07-05",
                TripType = TripType.RoundTrip
            };

            var result = await _service.SearchFlightsAsync(request);

            Assert.NotNull(result);
            Assert.NotEmpty(result.OutboundFlights);
            Assert.NotNull(result.ReturnFlights);
        }

        [Fact]
        public async Task SearchFlightsAsync_ShouldThrow_WhenReturnDateMissing()
        {
            SetupMultiResponses();

            var request = new FlightSearchRequest
            {
                DepartureCity = "Cairo",
                ArrivalCity = "Paris",
                DepartureDate = "2026-07-01",
                TripType = TripType.RoundTrip
            };

            await Assert.ThrowsAsync<Exception>(() =>
                _service.SearchFlightsAsync(request));
        }

        // ============================================================
        // Helpers
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
                        : new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
                });
        }

        
        private void SetupMultiResponses()
        {
            var callCount = 0;

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;

                    // 1 & 2 → AirLabs
                    if (callCount <= 2)
                    {
                        var iata = callCount == 1 ? "CAI" : "CDG";

                        var responseObj = new
                        {
                            response = new
                            {
                                airports = new[]
                                {
                                    new { iata_code = iata }
                                }
                            }
                        };

                        return CreateResponse(HttpStatusCode.OK, responseObj);
                    }

                    // 3 & 4 → AeroDataBox
                    var flightsObj = new
                    {
                        departures = new[]
                        {
                            new
                            {
                                number = "MS700",
                                airline = new { name = "EgyptAir" },
                                arrival = new
                                {
                                    airport = new { iata = "CDG" },
                                    scheduledTime = new { local = "2026-07-01T12:00:00" }
                                },
                                departure = new
                                {
                                    scheduledTime = new { local = "2026-07-01T08:00:00" }
                                },
                                status = "Scheduled",
                                aircraft = new { model = "Boeing 737" }
                            }
                        }
                    };

                    return CreateResponse(HttpStatusCode.OK, flightsObj);
                });
        }

        private HttpResponseMessage CreateResponse(HttpStatusCode status, object content)
        {
            return new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(
                    JsonSerializer.Serialize(content),
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
