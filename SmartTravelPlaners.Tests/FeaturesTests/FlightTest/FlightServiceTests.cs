using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly Mock<ILogger<FlightService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly FlightService _service;

        public FlightServiceTests()
        {
            _handlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_handlerMock.Object);
            _loggerMock = new Mock<ILogger<FlightService>>();

            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["FlightApiSettings:AeroApiKey"]).Returns("test-aero-key");
            _configurationMock.Setup(c => c["FlightApiSettings:AirLabsApiKey"]).Returns("test-airlabs-key");

            _service = new FlightService(_httpClient, _loggerMock.Object, _configurationMock.Object);
        }

        // ============================================================
        // GetAirportCodesAsync
        // ============================================================

        [Fact]
        public async Task GetAirportCodesAsync_ShouldReturnCodes_WhenApiSuccess()
        {
            var responseObj = new
            {
                response = new
                {
                    airports = new[]
                    {
                        new { iata_code = "CAI", icao_code = "HECA" }
                    }
                }
            };

            SetupResponse(HttpStatusCode.OK, responseObj);

            var result = await _service.GetAirportCodesAsync("Alexandria");

            Assert.Equal("CAI", result.Iata);
            Assert.Equal("HECA", result.Icao);
        }

        [Fact]
        public async Task GetAirportCodesAsync_ShouldReturnFromFallback_WhenCountryKnown()
        {
            // No HTTP call should happen because "Egypt" exists in the fallback map
            var result = await _service.GetAirportCodesAsync("Egypt");

            Assert.Equal("CAI", result.Iata);
            Assert.Equal("HECA", result.Icao);

            _handlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetAirportCodesAsync_ShouldThrow_WhenApiFails()
        {
            SetupResponse(HttpStatusCode.BadRequest, null);

            await Assert.ThrowsAsync<Exception>(() =>
                _service.GetAirportCodesAsync("Alexandria"));
        }

        [Fact]
        public async Task GetAirportCodesAsync_ShouldThrow_WhenCityNameEmpty()
        {
            await Assert.ThrowsAsync<Exception>(() =>
                _service.GetAirportCodesAsync(""));
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
                DepartureCity = "Alexandria",
                ArrivalCity = "Marseille",
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
                DepartureCity = "Alexandria",
                ArrivalCity = "Marseille",
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
                DepartureCity = "Alexandria",
                ArrivalCity = "Marseille",
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
                        ? new StringContent("")
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

                    // 1 & 2 → AirLabs (city -> iata/icao resolution)
                    if (callCount <= 2)
                    {
                        var (iata, icao) = callCount == 1 ? ("CAI", "HECA") : ("CDG", "LFPG");

                        var responseObj = new
                        {
                            response = new
                            {
                                airports = new[]
                                {
                                    new { iata_code = iata, icao_code = icao }
                                }
                            }
                        };

                        return CreateResponse(HttpStatusCode.OK, responseObj);
                    }

                    // 3+ → AeroDataBox (morning/evening windows, for outbound and possibly return)
                    var flightsObj = new
                    {
                        departures = new[]
                        {
                            new
                            {
                                number = "MS700",
                                airline = new { name = "EgyptAir", iata = "MS" },
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
                                isCargo = false
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