using System.Text.Json;
using SmartTravelPlaners.BLL.Features.Place.Plugins;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Plugins;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Plugins;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.Services
{
    public class TripOrchestratorService : ITripOrchestratorService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly HotelPlugin _hotelPlugin;
        private readonly FlightPlugin _flightPlugin;
        private readonly PlacesPlugin _placesPlugin;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TripOrchestratorService(
            IUnitOfWork unitOfWork,
            HotelPlugin hotelPlugin,
            FlightPlugin flightPlugin,
            PlacesPlugin placesPlugin)
        {
            _unitOfWork = unitOfWork;
            _hotelPlugin = hotelPlugin;
            _flightPlugin = flightPlugin;
            _placesPlugin = placesPlugin;
        }

        public async Task<TripPlanDto> BuildTripPlanAsync(Guid tripId)
        {
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");

            var checkIn = trip.StartDate.ToString("yyyy-MM-dd");
            var checkOut = trip.EndDate.ToString("yyyy-MM-dd");
            var numberOfNights = trip.EndDate.DayNumber - trip.StartDate.DayNumber;
            var numberOfDays = Math.Max(numberOfNights, 1);

            var hasOrigin = !string.IsNullOrWhiteSpace(trip.OriginCity);

            decimal hotelBudget, activitiesBudget, flightBudget = 0;

            if (hasOrigin)
            {
                hotelBudget = BudgetAllocator.HotelBudget(trip.BudgetTotal);
                flightBudget = BudgetAllocator.FlightBudget(trip.BudgetTotal);
                activitiesBudget = BudgetAllocator.ActivitiesBudget(trip.BudgetTotal);
            }
            else
            {
                var (hb, ab) = BudgetAllocator.WithoutFlight(trip.BudgetTotal);
                hotelBudget = hb;
                activitiesBudget = ab;
            }

            var hotelDto = await SelectHotelAsync(trip, checkIn, checkOut, hotelBudget, numberOfNights);


            FlightDto? flightDto = null;

            if (hasOrigin)
            {
                try
                {
                    flightDto = await SelectFlightAsync(trip, checkIn);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Flight failed but ignored: {ex.Message}");
                    flightDto = null;
                }
            }

            var dayPlans = await BuildDayPlansAsync(trip, numberOfDays, activitiesBudget);

            await PersistPlanAsync(trip, hotelDto, flightDto, dayPlans);

            var estimatedTotal =
                (decimal)(hotelDto?.Price.PricePerNight ?? 0) * Math.Max(numberOfNights, 1)
                + (flightDto != null ? flightBudget : 0)
                + dayPlans.Sum(d => d.Activities.Sum(a => a.EstimatedCost));

            var plan = new TripPlanDto
            {
                TripId = trip.Id,
                Destination = trip.Destination,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                BudgetTotal = trip.BudgetTotal,
                EstimatedTotalCost = estimatedTotal,
                Hotel = hotelDto is null ? null : MapHotel(hotelDto),
                Flight = flightDto is null ? null : MapFlight(flightDto),
                Days = dayPlans,
                Summary = BuildSummary(trip, hotelDto, flightDto, dayPlans)
            };

            return plan;
        }

        private async Task<GoogleHotelDto?> SelectHotelAsync(
            Trip trip, string checkIn, string checkOut, decimal hotelBudget, int numberOfNights)
        {
            var maxPricePerNight = numberOfNights <= 0
                ? hotelBudget
                : hotelBudget / numberOfNights;

            var filteredJson = await _hotelPlugin.FilterHotelsAsync(
                trip.Destination, checkIn, checkOut,
                maxPrice: maxPricePerNight,
                minRating: 3.5,
                amenities: new List<string>());

            var hotels = TryDeserialize<List<GoogleHotelDto>>(filteredJson) ?? new();

            if (hotels.Count == 0)
            {
                var searchJson = await _hotelPlugin.SearchHotelsAsync(
                    trip.Destination, checkIn, checkOut, trip.NumTravelers, 0);

                hotels = TryDeserialize<List<GoogleHotelDto>>(searchJson) ?? new();
            }

            var withinBudget = hotels
                .Where(h => h.Price.PricePerNight.HasValue &&
                            h.Price.PricePerNight.Value <= (double)maxPricePerNight)
                .OrderByDescending(h => h.Rating.Value ?? 0)
                .FirstOrDefault();

            return withinBudget ?? hotels.OrderByDescending(h => h.Rating.Value ?? 0).FirstOrDefault();
        }

        private async Task<FlightDto?> SelectFlightAsync(Trip trip, string departureDate)
        {
            try
            {
                var json = await _flightPlugin.SearchFlightsAsync(
                    departureCity: trip.OriginCity ?? "",
                    arrivalCity: trip.Destination,
                    departureDate: departureDate,
                    tripType: "OneWay");

                var result = TryDeserialize<FlightSearchResult>(json);

                return result?.OutboundFlights.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SelectFlightAsync failed: {ex.Message}");
                return null;
            }
        }

        private async Task<List<DayPlanDto>> BuildDayPlansAsync(
            Trip trip, int numberOfDays, decimal activitiesBudget)
        {
            var dailyBudget = numberOfDays > 0 ? activitiesBudget / numberOfDays : 0;
            var usedPlaceIds = new HashSet<string>();
            var days = new List<DayPlanDto>();

            var attractions = await SearchPlaces(trip.Destination, "attraction");
            var restaurants = await SearchPlaces(trip.Destination, "restaurant");
            var cafes = await SearchPlaces(trip.Destination, "cafe");

            for (int dayNumber = 1; dayNumber <= numberOfDays; dayNumber++)
            {
                var date = trip.StartDate.AddDays(dayNumber - 1);
                var activities = new List<ActivityPlanDto>();

                AddActivityIfAvailable(activities, attractions, usedPlaceIds, "Morning", "Attraction", dailyBudget * 0.4m);
                AddActivityIfAvailable(activities, restaurants, usedPlaceIds, "Lunch", "Restaurant", dailyBudget * 0.3m);
                AddActivityIfAvailable(activities, cafes, usedPlaceIds, "Evening", "Cafe", dailyBudget * 0.3m);

                days.Add(new DayPlanDto
                {
                    DayNumber = dayNumber,
                    Date = date,
                    BudgetAllocated = dailyBudget,
                    Activities = activities
                });
            }

            return days;
        }

        private async Task<List<PlaceDto>> SearchPlaces(string city, string category)
        {
            try
            {
                return await _placesPlugin.SearchWithImages(city, category);
            }
            catch
            {
                return new List<PlaceDto>();
            }
        }

        private static void AddActivityIfAvailable(
            List<ActivityPlanDto> activities,
            List<PlaceDto> pool,
            HashSet<string> usedPlaceIds,
            string timeSlot,
            string type,
            decimal estimatedCost)
        {
            var place = pool
     .OrderBy(x => Guid.NewGuid())
     .FirstOrDefault();
            if (place is null) return;

            usedPlaceIds.Add(place.FsqPlaceId);

            activities.Add(new ActivityPlanDto
            {
                Name = place.Name,
                Type = type,
                LocationName = place.Address,
                Lat = place.Latitude,
                Lng = place.Longitude,
                TimeSlot = timeSlot,
                EstimatedCost = estimatedCost,
                PlaceId = place.FsqPlaceId
            });
        }

        private async Task PersistPlanAsync(
            Trip trip,
            GoogleHotelDto? hotel,
            FlightDto? flight,
            List<DayPlanDto> dayPlans)
        {
            if (hotel is not null)
            {
                var hotelEntity = new SmartTravelPlaners.DAL.Entities.Hotel
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    Name = hotel.Name,
                    Address = hotel.Location.Address,
                    Lat = hotel.Location.Latitude,
                    Lng = hotel.Location.Longitude,
                    CheckIn = trip.StartDate,
                    CheckOut = trip.EndDate,
                    PricePerNight = (decimal)(hotel.Price.PricePerNight ?? 0),
                    Stars = (int)Math.Round(hotel.Rating.Value ?? 0),
                    Status = BookingStatus.Suggested
                };

                await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>().AddAsync(hotelEntity);
            }

            if (flight is not null)
            {
                var flightEntity = new SmartTravelPlaners.DAL.Entities.Flight
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    Airline = flight.AirlineName,
                    FlightNumber = flight.FlightNumber,
                    Origin = flight.DepartureAirport,
                    Destination = flight.ArrivalAirport,
                    DepartureTime = ParseDateTime(flight.DepartureTime),
                    ArrivalTime = ParseDateTime(flight.ArrivalTime),
                    Price = 0,
                    Status = BookingStatus.Suggested
                };

                await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>().AddAsync(flightEntity);
            }

            foreach (var dayPlan in dayPlans)
            {
                var tripDay = new TripDay
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    DayNumber = dayPlan.DayNumber,
                    Date = dayPlan.Date,
                    BudgetAllocated = dayPlan.BudgetAllocated,
                    BudgetSpent = 0
                };

                foreach (var activity in dayPlan.Activities)
                {
                    tripDay.Activities.Add(new Activity
                    {
                        Id = Guid.NewGuid(),
                        TripDayId = tripDay.Id,
                        Name = activity.Name,
                        Type = ParseActivityType(activity.Type),
                        LocationName = activity.LocationName,
                        Lat = activity.Lat,
                        Lng = activity.Lng,
                        EstimatedCost = activity.EstimatedCost,
                        Status = ActivityStatus.Suggested,
                        PlaceId = activity.PlaceId
                    });
                }

                await _unitOfWork.Repository<TripDay>().AddAsync(tripDay);
            }

            trip.Status = TripStatus.Confirmed;
            await _unitOfWork.CompleteAsync();
        }

        private static TripHotelDto MapHotel(GoogleHotelDto hotel) => new()
        {
            Name = hotel.Name,
            PricePerNight = hotel.Price.PricePerNight,
            Rating = hotel.Rating.Value,
            Address = hotel.Location.Address,
            Images = hotel.Images
        };

        private static TripFlightDto MapFlight(FlightDto flight) => new()
        {
            AirlineName = flight.AirlineName,
            FlightNumber = flight.FlightNumber,
            DepartureAirport = flight.DepartureAirport,
            ArrivalAirport = flight.ArrivalAirport,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime
        };

        private static string BuildSummary(
            Trip trip, GoogleHotelDto? hotel, FlightDto? flight, List<DayPlanDto> days)
        {
            var hotelPart = hotel is not null ? $"staying at {hotel.Name}" : "no hotel selected";
            var flightPart = flight is not null ? $" with a flight via {flight.AirlineName}" : "";
            return $"Trip to {trip.Destination} ({trip.StartDate} - {trip.EndDate}), {hotelPart}{flightPart}, " +
                   $"with {days.Count} day(s) of planned activities.";
        }

        private static ActivityType ParseActivityType(string type)
            => Enum.TryParse<ActivityType>(type, ignoreCase: true, out var result)
                ? result
                : ActivityType.Other;

        private static DateTime ParseDateTime(string value)
            => DateTime.TryParse(value, out var dt) ? dt : DateTime.UtcNow;

        private static T? TryDeserialize<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
            catch { return default; }
        }
    }
}