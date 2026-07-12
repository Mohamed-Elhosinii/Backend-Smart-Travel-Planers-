using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;
using SmartTravelPlaners.PL.Controllers;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class FlightControllerTests
    {
        private readonly Mock<IFlightService> _serviceMock;
        private readonly Mock<ILogger<FlightController>> _loggerMock;
        private readonly FlightController _controller;

        public FlightControllerTests()
        {
            _serviceMock = new Mock<IFlightService>();
            _loggerMock = new Mock<ILogger<FlightController>>();
            _controller = new FlightController(_serviceMock.Object, _loggerMock.Object);
          
        }

        private FlightSearchResult MakeResult(bool roundTrip = false) => new FlightSearchResult
        {
            DepartureIata = "CAI",
            ArrivalIata = "DXB",
            OutboundFlights = new List<FlightDto>
            {
                new() { AirlineName = "EgyptAir", FlightNumber = "MS700",
                        DepartureAirport = "CAI", ArrivalAirport = "DXB",
                        DepartureTime = "2026-06-20T08:00:00", ArrivalTime = "2026-06-20T12:00:00" }
            },
            ReturnFlights = roundTrip ? new List<FlightDto>
            {
                new() { AirlineName = "EgyptAir", FlightNumber = "MS701",
                        DepartureAirport = "DXB", ArrivalAirport = "CAI",
                        DepartureTime = "2026-06-25T08:00:00", ArrivalTime = "2026-06-25T12:00:00" }
            } : null
        };

        // ============================================================
        // SearchFlights — OneWay
        // ============================================================

        [Fact]
        public async Task SearchFlights_ShouldReturn200_WhenOneWay()
        {
            _serviceMock.Setup(s => s.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
                .ReturnsAsync(MakeResult());

            var result = await _controller.SearchFlights("Cairo", "Dubai", "2026-06-20", TripType.OneWay) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // SearchFlights — RoundTrip
        // ============================================================

        [Fact]
        public async Task SearchFlights_ShouldReturn200_WhenRoundTrip()
        {
            _serviceMock.Setup(s => s.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
                .ReturnsAsync(MakeResult(roundTrip: true));

            var result = await _controller.SearchFlights("Cairo", "Dubai", "2026-06-20",
                TripType.RoundTrip, "2026-06-25") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // SearchFlights — RoundTrip بدون ReturnDate
        // ============================================================

        [Fact]
        public async Task SearchFlights_ShouldReturn400_WhenRoundTripWithoutReturnDate()
        {
            var result = await _controller.SearchFlights("Cairo", "Dubai", "2026-06-20",
                TripType.RoundTrip, null) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // SearchFlights — Service throws exception
        // ============================================================

        [Fact]
        public async Task SearchFlights_ShouldReturn500_WhenServiceThrows()
        {
            _serviceMock.Setup(s => s.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
                .ThrowsAsync(new Exception("API Error"));

            var result = await _controller.SearchFlights() as ObjectResult;

            Assert.NotNull(result);
            Assert.Equal(500, result!.StatusCode);
        }
    }
}
