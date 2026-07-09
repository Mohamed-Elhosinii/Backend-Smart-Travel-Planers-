using Moq;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;
using SmartTravelPlaners.BLL.Features.Flight.Plugins;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;
using SmartTravelPlaners.BLL.Features.Hotel.Plugins;
using SmartTravelPlaners.BLL.Features.Orchestrator.Services;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;
using SmartTravelPlaners.BLL.Features.Place.Plugins;
using SmartTravelPlaners.BLL.Features.Weather.DTOs;
using SmartTravelPlaners.BLL.Features.Weather.Interfaces;
using SmartTravelPlaners.BLL.Features.Weather.Plugins;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using System.Linq.Expressions;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Orchestrator
{
    public class TripOrchestratorServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<IHotelApiService> _hotelApiMock;
        private readonly Mock<IFlightService> _flightApiMock;
        private readonly Mock<IPlacesApiService> _placesApiMock;
        private readonly Mock<IWeatherApiService> _weatherApiMock;
        private readonly HotelPlugin _hotelPlugin;
        private readonly FlightPlugin _flightPlugin;
        private readonly PlacesPlugin _placesPlugin;
        private readonly WeatherPlugin _weatherPlugin;
        private readonly Mock<IGenericRepository<DAL.Entities.Hotel>> _hotelRepoMock;
        private readonly Mock<IGenericRepository<DAL.Entities.Flight>> _flightRepoMock;
        private readonly Mock<IGenericRepository<TripDay>> _tripDayRepoMock;
        private readonly Mock<IGenericRepository<Activity>> _activityRepoMock;
        private readonly Mock<IGenericRepository<WeatherDay>> _weatherDayRepoMock;
        private readonly TripOrchestratorService _service;

        public TripOrchestratorServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _hotelApiMock = new Mock<IHotelApiService>();
            _flightApiMock = new Mock<IFlightService>();
            _placesApiMock = new Mock<IPlacesApiService>();
            _weatherApiMock = new Mock<IWeatherApiService>();

            _hotelPlugin = new HotelPlugin(
                _hotelApiMock.Object,
                new Mock<SmartTravelPlaners.BLL.Features.Hotel.Interfaces.IPlaceResolverService>().Object,
                new Mock<SmartTravelPlaners.BLL.Features.Hotel.Interfaces.IHotelSearchService>().Object);
            _flightPlugin = new FlightPlugin(_flightApiMock.Object);
            _placesPlugin = new PlacesPlugin(_placesApiMock.Object, new Mock<Microsoft.Extensions.Logging.ILogger<PlacesPlugin>>().Object);
            _weatherPlugin = new WeatherPlugin(_weatherApiMock.Object);

            _hotelRepoMock = new Mock<IGenericRepository<DAL.Entities.Hotel>>();
            _flightRepoMock = new Mock<IGenericRepository<DAL.Entities.Flight>>();
            _tripDayRepoMock = new Mock<IGenericRepository<TripDay>>();
            _activityRepoMock = new Mock<IGenericRepository<Activity>>();
            _weatherDayRepoMock = new Mock<IGenericRepository<WeatherDay>>();

            _uowMock.Setup(u => u.Repository<DAL.Entities.Hotel>()).Returns(_hotelRepoMock.Object);
            _uowMock.Setup(u => u.Repository<DAL.Entities.Flight>()).Returns(_flightRepoMock.Object);
            _uowMock.Setup(u => u.Repository<TripDay>()).Returns(_tripDayRepoMock.Object);
            _uowMock.Setup(u => u.Repository<Activity>()).Returns(_activityRepoMock.Object);
            _uowMock.Setup(u => u.Repository<WeatherDay>()).Returns(_weatherDayRepoMock.Object);
            _uowMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _service = new TripOrchestratorService(
                _uowMock.Object,
                _hotelPlugin,
                _flightPlugin,
                _placesPlugin,
                _weatherPlugin,
                new Mock<Microsoft.Extensions.Logging.ILogger<TripOrchestratorService>>().Object);
        }

        private Trip MakeTrip(bool hasOrigin = true) => new Trip
        {
            Id = Guid.NewGuid(),
            Destination = "Paris",
            OriginCity = hasOrigin ? "Cairo" : null,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
            BudgetTotal = 5000,
            NumTravelers = 2,
            Days = new List<TripDay>(),
            Hotels = new List<DAL.Entities.Hotel>(),
            Flights = new List<DAL.Entities.Flight>(),
            WeatherDays = new List<WeatherDay>(),
            Preferences= new List<DAL.Entities.TripPreference>()
        };

        private void SetupTripWithDetails(Trip trip)
        {
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
        }

        private void SetupEmptyHotels()
        {
            _hotelApiMock.Setup(h => h.FilterHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<double>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new List<GoogleHotelDto>());
            _hotelApiMock
    .Setup(h => h.GetAvailableHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
    .ReturnsAsync(new List<GoogleHotelDto>());
        }

        private void SetupEmptyFlights()
        {
            _flightApiMock.Setup(f => f.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
                .ReturnsAsync(new FlightSearchResult { OutboundFlights = new() });
        }

        private void SetupEmptyWeather()
        {
            _weatherApiMock.Setup(w => w.GetWeatherForTripAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new VisualCrossingResponseDto { Days = new() });
        }
        private void SetupEmptyPlaces()
        {
            _placesApiMock.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<PlaceDto>());
        }

        // ============================================================
        // BuildTripPlanAsync
        // ============================================================

        [Fact]
        public async Task BuildTripPlanAsync_ShouldThrow_WhenTripNotFound()
        {
            var fakeId = Guid.NewGuid();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(fakeId)).ReturnsAsync((Trip?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.BuildTripPlanAsync(fakeId));
        }

        [Fact]
        public async Task BuildTripPlanAsync_ShouldReturnPlan_WhenTripHasNoOrigin()
        {
            var trip = MakeTrip(hasOrigin: false);
            SetupTripWithDetails(trip);
            SetupEmptyHotels();
            SetupEmptyFlights();
            SetupEmptyWeather();
            SetupEmptyPlaces();

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            Assert.Equal(trip.Id, result.TripId);
            Assert.Equal("Paris", result.Destination);
            Assert.Null(result.Flight);
        }

        [Fact]
        public async Task BuildTripPlanAsync_ShouldReturnPlan_WithHotel_WhenHotelAvailable()
        {
            var trip = MakeTrip(hasOrigin: false);
            SetupTripWithDetails(trip);
            SetupEmptyFlights();
            SetupEmptyWeather();
            SetupEmptyPlaces();

            _hotelApiMock.Setup(h => h.FilterHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<double>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new List<GoogleHotelDto>
                {
                    new()
                    {
                        Name = "Paris Grand Hotel",
                        Location = new() { Address = "1 Rue de Paris", Latitude = 48.8, Longitude = 2.3 },
                        Price = new() { PricePerNight = 100.0 },
                        Rating = new() { Value = 4.5 },
                        Images = new()
                    }
                });

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            Assert.NotNull(result.Hotel);
            Assert.Equal("Paris Grand Hotel", result.Hotel!.Name);
            _hotelRepoMock.Verify(r => r.AddAsync(It.IsAny<DAL.Entities.Hotel>()), Times.Once);
        }

        [Fact]
        public async Task BuildTripPlanAsync_ShouldReturnPlan_WithFlight_WhenOriginExists()
        {
            var trip = MakeTrip(hasOrigin: true);
            SetupTripWithDetails(trip);
            SetupEmptyHotels();
            SetupEmptyWeather();
            SetupEmptyPlaces();

            _flightApiMock.Setup(f => f.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
                
                .ReturnsAsync(new FlightSearchResult
                {
                    OutboundFlights = new List<FlightDto>
                    {
                        new()
                        {
                            AirlineName = "EgyptAir",
                            FlightNumber = "MS700",
                            DepartureAirport = "CAI",
                            ArrivalAirport = "CDG",
                            DepartureTime = "2025-01-10T08:00:00",
                            ArrivalTime = "2025-01-10T12:00:00"
                        }
                    }
                });

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            Assert.NotNull(result.Flight);
            Assert.Equal("EgyptAir", result.Flight!.AirlineName);
            _flightRepoMock.Verify(r => r.AddAsync(It.IsAny<DAL.Entities.Flight>()), Times.Once);
        }

        [Fact]
        public async Task BuildTripPlanAsync_ShouldClearExistingPlan_BeforeBuilding()
        {
            var trip = MakeTrip(hasOrigin: false);
            trip.Hotels.Add(new DAL.Entities.Hotel { Id = Guid.NewGuid(), TripId = trip.Id, Name = "Old Hotel" });
            trip.Days.Add(new TripDay { Id = Guid.NewGuid(), TripId = trip.Id, DayNumber = 1, Activities = new List<Activity>() });

            SetupTripWithDetails(trip);
            SetupEmptyHotels();
            SetupEmptyWeather();
            SetupEmptyPlaces();

            await _service.BuildTripPlanAsync(trip.Id);

            _hotelRepoMock.Verify(r => r.DeleteRange(It.IsAny<IEnumerable<DAL.Entities.Hotel>>()), Times.Once);
            _tripDayRepoMock.Verify(r => r.DeleteRange(It.IsAny<IEnumerable<TripDay>>()), Times.Once);
        }

        [Fact]
        public async Task BuildTripPlanAsync_ShouldBuildDayPlans_BasedOnTripDuration()
        {
            var trip = MakeTrip(hasOrigin: false); // 4 days
            SetupTripWithDetails(trip);
            SetupEmptyHotels();
            SetupEmptyWeather();

            _placesApiMock.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<PlaceDto>
                {
                    new() { FsqPlaceId = "1", Name = "Eiffel Tower", Address = "Paris", Latitude = 48.8, Longitude = 2.3, Images = new() },
                    new() { FsqPlaceId = "2", Name = "Le Bistro", Address = "Paris", Latitude = 48.8, Longitude = 2.3, Images = new() },
                    new() { FsqPlaceId = "3", Name = "Cafe de Paris", Address = "Paris", Latitude = 48.8, Longitude = 2.3, Images = new() },
                });

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            Assert.Equal(4, result.Days.Count);
            _tripDayRepoMock.Verify(r => r.AddAsync(It.IsAny<TripDay>()), Times.Exactly(4));
        }

        // ============================================================
        // GetCurrentPlanAsync
        // ============================================================

        [Fact]
        public async Task GetCurrentPlanAsync_ShouldReturnPlan_WhenTripExists()
        {
            var trip = MakeTrip();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
            _hotelRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Hotel, bool>>>()))
                .ReturnsAsync(new List<DAL.Entities.Hotel>());
            _flightRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Flight, bool>>>()))
                .ReturnsAsync(new List<DAL.Entities.Flight>());
            _tripDayRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TripDay, bool>>>()))
                .ReturnsAsync(new List<TripDay>());

            var result = await _service.GetCurrentPlanAsync(trip.Id);

            Assert.NotNull(result);
            Assert.Equal(trip.Id, result.TripId);
            Assert.Equal("Paris", result.Destination);
        }

        [Fact]
        public async Task GetCurrentPlanAsync_ShouldThrow_WhenTripNotFound()
        {
            var fakeId = Guid.NewGuid();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(fakeId)).ReturnsAsync((Trip?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.GetCurrentPlanAsync(fakeId));
        }

        [Fact]
        public async Task GetCurrentPlanAsync_ShouldReturnHotel_WhenHotelExists()
        {
            var trip = MakeTrip();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
            _hotelRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Hotel, bool>>>()))
                .ReturnsAsync(new List<DAL.Entities.Hotel>
                {
                    new() { Id = Guid.NewGuid(), TripId = trip.Id, Name = "Hilton Paris", PricePerNight = 200, Stars = 5 }
                });
            _flightRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Flight, bool>>>()))
                .ReturnsAsync(new List<DAL.Entities.Flight>());
            _tripDayRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TripDay, bool>>>()))
                .ReturnsAsync(new List<TripDay>());

            var result = await _service.GetCurrentPlanAsync(trip.Id);

            Assert.NotNull(result.Hotel);
            Assert.Equal("Hilton Paris", result.Hotel!.Name);
        }

        [Fact]
        public async Task GetCurrentPlanAsync_ShouldReturnFlight_WhenFlightExists()
        {
            var trip = MakeTrip();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
            _hotelRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Hotel, bool>>>()))
                .ReturnsAsync(new List<DAL.Entities.Hotel>());
            _flightRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Flight, bool>>>()))
                .ReturnsAsync(new List<DAL.Entities.Flight>
                {
                    new() { Id = Guid.NewGuid(), TripId = trip.Id, Airline = "EgyptAir", FlightNumber = "MS700",
                            Origin = "CAI", Destination = "CDG",
                            DepartureTime = DateTime.Now, ArrivalTime = DateTime.Now.AddHours(5) }
                });
            _tripDayRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TripDay, bool>>>()))
                .ReturnsAsync(new List<TripDay>());

            var result = await _service.GetCurrentPlanAsync(trip.Id);

            Assert.NotNull(result.Flight);
            Assert.Equal("EgyptAir", result.Flight!.AirlineName);
        }

        // ============================================================
        // RegenerateHotelAsync
        // ============================================================

        [Fact]
        public async Task RegenerateHotelAsync_ShouldReturnNull_WhenNoHotelsAvailable()
        {
            var trip = MakeTrip();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
            _hotelApiMock.Setup(h => h.FilterHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<double>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new List<GoogleHotelDto>());
            _hotelApiMock.Setup(h => h.GetAvailableHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<GoogleHotelDto>());

            var result = await _service.RegenerateHotelAsync(trip.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task RegenerateHotelAsync_ShouldThrow_WhenTripNotFound()
        {
            var fakeId = Guid.NewGuid();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(fakeId)).ReturnsAsync((Trip?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.RegenerateHotelAsync(fakeId));
        }

        [Fact]
        public async Task RegenerateHotelAsync_ShouldUpdateExistingHotel_WhenHotelExists()
        {
            var trip = MakeTrip();
            var existingHotel = new DAL.Entities.Hotel { Id = Guid.NewGuid(), TripId = trip.Id, Name = "Old Hotel" };
            trip.Hotels.Add(existingHotel);
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);

            _hotelApiMock.Setup(h => h.FilterHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<double>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new List<GoogleHotelDto>
                {
                    new()
                    {
                        Name = "New Hotel",
                        Location = new() { Address = "123 St", Latitude = 48.8, Longitude = 2.3 },
                        Price = new() { PricePerNight = 100.0 },
                        Rating = new() { Value = 4.5 },
                        Images = new()
                    }
                });

            var result = await _service.RegenerateHotelAsync(trip.Id);

            Assert.NotNull(result);
            Assert.Equal("New Hotel", result!.Name);
            _hotelRepoMock.Verify(r => r.Update(It.IsAny<DAL.Entities.Hotel>()), Times.Once);
        }

       
        [Fact]
        public async Task RegenerateHotelAsync_ShouldAddNewHotel_WhenNoExistingHotel()
        {
            var trip = MakeTrip();
           

            _uowMock
                .Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id))
                .ReturnsAsync(trip);

            _hotelApiMock
                .Setup(h => h.FilterHotelsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal>(),
                    It.IsAny<double>(),
                    It.IsAny<List<string>>()))
                .ReturnsAsync(new List<GoogleHotelDto>
                {
            new()
            {
                Name = "Brand New Hotel",
                Location = new() { Address = "456 Ave", Latitude = 48.9, Longitude = 2.4 },
                Price = new() { PricePerNight = 150.0 },
                Rating = new() { Value = 4.0 },
                Images = new()
            }
                });

            DAL.Entities.Hotel? savedHotel = null;

            _hotelRepoMock
                .Setup(r => r.AddAsync(It.IsAny<DAL.Entities.Hotel>()))
                .Callback<DAL.Entities.Hotel>(h => savedHotel = h)
                .Returns(Task.CompletedTask);

            var result = await _service.RegenerateHotelAsync(trip.Id);

            Assert.NotNull(result);
            Assert.Equal("Brand New Hotel", result!.Name);

            Assert.NotNull(savedHotel);
            Assert.Equal("Brand New Hotel", savedHotel!.Name);
        }

        // ============================================================
        // RegenerateFlightAsync
        // ============================================================

        [Fact]
        public async Task RegenerateFlightAsync_ShouldReturnNull_WhenNoOriginCity()
        {
            var trip = MakeTrip(hasOrigin: false);
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);

            var result = await _service.RegenerateFlightAsync(trip.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task RegenerateFlightAsync_ShouldThrow_WhenTripNotFound()
        {
            var fakeId = Guid.NewGuid();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(fakeId)).ReturnsAsync((Trip?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.RegenerateFlightAsync(fakeId));
        }

        [Fact]
        public async Task RegenerateFlightAsync_ShouldAddFlight_WhenNoExistingFlight()
        {
            var trip = MakeTrip(hasOrigin: true);
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);

            _flightApiMock.Setup(f => f.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
               
                .ReturnsAsync(new FlightSearchResult
                {
                    OutboundFlights = new List<FlightDto>
                    {
                        new() { AirlineName = "EgyptAir", FlightNumber = "MS700",
                                DepartureAirport = "CAI", ArrivalAirport = "CDG",
                                DepartureTime = "2025-01-10T08:00:00", ArrivalTime = "2025-01-10T12:00:00" }
                    }
                });

            var result = await _service.RegenerateFlightAsync(trip.Id);

            Assert.NotNull(result);
            Assert.Equal("EgyptAir", result!.AirlineName);
            _flightRepoMock.Verify(r => r.AddAsync(It.IsAny<DAL.Entities.Flight>()), Times.Once);
        }

       

        [Fact]
        public async Task RegenerateFlightAsync_ShouldReturnNull_WhenSearchFails()
        {
            var trip = MakeTrip(hasOrigin: true);
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
            _flightApiMock.Setup(f => f.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
               .ReturnsAsync(new FlightSearchResult { OutboundFlights = new() });

            var result = await _service.RegenerateFlightAsync(trip.Id);

            Assert.Null(result);
        }

        // ============================================================
        // RegenerateDayActivitiesAsync
        // ============================================================

        [Fact]
        public async Task RegenerateDayActivitiesAsync_ShouldThrow_WhenDayNotFound()
        {
            var tripId = Guid.NewGuid();
            _tripDayRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TripDay, bool>>>()))
                .ReturnsAsync(new List<TripDay>());

            await Assert.ThrowsAsync<Exception>(() => _service.RegenerateDayActivitiesAsync(tripId, 1));
        }

        [Fact]
        public async Task RegenerateDayActivitiesAsync_ShouldReturnEmpty_WhenNoPlacesFound()
        {
            var trip = MakeTrip();
            var tripDay = new TripDay { Id = Guid.NewGuid(), TripId = trip.Id, DayNumber = 1, Activities = new List<Activity>() };

            _tripDayRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TripDay, bool>>>()))
                .ReturnsAsync(new List<TripDay> { tripDay });
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
            _activityRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Activity, bool>>>()))
                .ReturnsAsync(new List<Activity>());
            _placesApiMock.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<PlaceDto>());

            var result = await _service.RegenerateDayActivitiesAsync(trip.Id, 1);

            Assert.Empty(result);
        }

        // ============================================================
        // SyncDayPlansAsync
        // ============================================================

        [Fact]
        public async Task SyncDayPlansAsync_ShouldThrow_WhenTripNotFound()
        {
            var fakeId = Guid.NewGuid();
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(fakeId)).ReturnsAsync((Trip?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.SyncDayPlansAsync(fakeId));
        }

        [Fact]
        public async Task SyncDayPlansAsync_ShouldUpdateDates_WhenDaysCountMatches()
        {
            var trip = MakeTrip();
            var days = Enumerable.Range(1, 4).Select(i => new TripDay
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                DayNumber = i,
                Date = trip.StartDate,
                Activities = new List<Activity>()
            }).ToList();

            trip.Days = days;
            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);

            await _service.SyncDayPlansAsync(trip.Id);

            _tripDayRepoMock.Verify(r => r.Update(It.IsAny<TripDay>()), Times.AtLeastOnce);
        }
    }
}

        // ============================================================
        // ConfirmedCost Tests
        // ============================================================

        [Fact]
        public async Task BuildTripPlanAsync_ShouldSetConfirmedCost_ToHotelCostOnly()
        {
            var trip = MakeTrip(hasOrigin: true); // has flight
            SetupTripWithDetails(trip);
            SetupEmptyFlights();
            SetupEmptyWeather();
            SetupEmptyPlaces();

            // Setup hotel with known price
            _hotelApiMock.Setup(h => h.FilterHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<double>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new List<GoogleHotelDto>
                {
                    new()
                    {
                        Name = "Test Hotel",
                        Location = new() { Address = "Test Address", Latitude = 48.8, Longitude = 2.3 },
                        Price = new() { PricePerNight = 500.0 }, // 500 per night
                        Rating = new() { Value = 4.5 },
                        Images = new()
                    }
                });

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            // Trip is 4 nights (from setup), so ConfirmedCost = 500 * 4 = 2000
            Assert.Equal(2000m, result.ConfirmedCost);
            // BudgetSpent should include hotel + flight + activities
            Assert.True(result.BudgetSpent >= result.ConfirmedCost, "BudgetSpent should be >= ConfirmedCost");
        }

        [Fact]
        public async Task GetCurrentPlanAsync_ShouldReturnConfirmedCost()
        {
            var trip = MakeTrip();
            var hotelCost = 1000m;
            var numberOfNights = 4;
            
            trip.Hotels.Add(new DAL.Entities.Hotel
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                Name = "Test Hotel",
                PricePerNight = hotelCost,
                Stars = 5,
                ImagesJson = "[]"
            });

            _uowMock.Setup(u => u.Trips.GetTripWithDetailsAsync(trip.Id)).ReturnsAsync(trip);
            _hotelRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Hotel, bool>>>()))
                .ReturnsAsync(trip.Hotels.ToList());
            _flightRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DAL.Entities.Flight, bool>>>()))
                .ReturnsAsync(new List<DAL.Entities.Flight>());
            _tripDayRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TripDay, bool>>>()))
                .ReturnsAsync(new List<TripDay>());

            var result = await _service.GetCurrentPlanAsync(trip.Id);

            Assert.NotNull(result);
            // ConfirmedCost = hotelCost * numberOfNights = 1000 * 4 = 4000
            Assert.Equal(4000m, result.ConfirmedCost);
        }

        [Fact]
        public async Task ConfirmedCost_ShouldBeZero_WhenNoHotel()
        {
            var trip = MakeTrip();
            SetupTripWithDetails(trip);
            SetupEmptyHotels();
            SetupEmptyFlights();
            SetupEmptyWeather();
            SetupEmptyPlaces();

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            Assert.Equal(0m, result.ConfirmedCost); // No hotel = 0 confirmed cost
        }

        [Fact]
        public async Task ConfirmedCost_ShouldNotIncludeFlight_EvenWhenFlightExists()
        {
            var trip = MakeTrip(hasOrigin: true);
            SetupTripWithDetails(trip);
            SetupEmptyHotels(); // No hotel
            SetupEmptyWeather();
            SetupEmptyPlaces();

            // Setup flight
            _flightApiMock.Setup(f => f.SearchFlightsAsync(It.IsAny<FlightSearchRequest>()))
                .ReturnsAsync(new FlightSearchResult
                {
                    OutboundFlights = new List<FlightDto>
                    {
                        new()
                        {
                            AirlineName = "EgyptAir",
                            FlightNumber = "MS700",
                            DepartureAirport = "CAI",
                            ArrivalAirport = "CDG",
                            DepartureTime = "2025-01-10T08:00:00",
                            ArrivalTime = "2025-01-10T12:00:00"
                        }
                    }
                });

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            Assert.NotNull(result.Flight); // Flight exists
            Assert.Equal(0m, result.ConfirmedCost); // But ConfirmedCost is still 0 (no hotel)
            Assert.True(result.BudgetSpent > 0, "BudgetSpent should include flight cost");
        }

        [Fact]
        public async Task ConfirmedCost_ShouldNotIncludeActivities()
        {
            var trip = MakeTrip(hasOrigin: false);
            SetupTripWithDetails(trip);
            SetupEmptyWeather();

            // Setup hotel
            _hotelApiMock.Setup(h => h.FilterHotelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<double>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new List<GoogleHotelDto>
                {
                    new()
                    {
                        Name = "Test Hotel",
                        Location = new() { Address = "Test", Latitude = 48.8, Longitude = 2.3 },
                        Price = new() { PricePerNight = 300.0 },
                        Rating = new() { Value = 4.0 },
                        Images = new()
                    }
                });

            // Setup activities with costs
            _placesApiMock.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<PlaceDto>
                {
                    new() { FsqPlaceId = "1", Name = "Place 1", Address = "Test", Latitude = 48.8, Longitude = 2.3, Images = new() },
                    new() { FsqPlaceId = "2", Name = "Place 2", Address = "Test", Latitude = 48.8, Longitude = 2.3, Images = new() },
                    new() { FsqPlaceId = "3", Name = "Place 3", Address = "Test", Latitude = 48.8, Longitude = 2.3, Images = new() }
                });

            var result = await _service.BuildTripPlanAsync(trip.Id);

            Assert.NotNull(result);
            var expectedConfirmedCost = 300m * 4; // hotel only: 300 * 4 nights = 1200
            Assert.Equal(expectedConfirmedCost, result.ConfirmedCost);
            
            // BudgetSpent should be higher (includes activities)
            var totalActivitiesCost = result.Days.Sum(d => d.Activities.Sum(a => a.EstimatedCost));
            Assert.True(totalActivitiesCost > 0, "Activities should have costs");
            Assert.Equal(expectedConfirmedCost + totalActivitiesCost, result.BudgetSpent);
        }
    }
}
