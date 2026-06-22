using System.Text.Json;
using SmartTravelPlaners.BLL.Features.Place.Plugins;
using SmartTravelPlaners.BLL.Features.Flight.DTOs;
using SmartTravelPlaners.BLL.Features.Flight.Plugins;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Plugins;
using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Weather.DTOs;
using SmartTravelPlaners.BLL.Features.Weather.Plugins;
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
        private readonly WeatherPlugin _weatherPlugin;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TripOrchestratorService(
            IUnitOfWork unitOfWork,
            HotelPlugin hotelPlugin,
            FlightPlugin flightPlugin,
            PlacesPlugin placesPlugin,
            WeatherPlugin weatherPlugin)
        {
            _unitOfWork = unitOfWork;
            _hotelPlugin = hotelPlugin;

            _flightPlugin = flightPlugin;
            _placesPlugin = placesPlugin;
            _weatherPlugin = weatherPlugin;
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

            // Fetch the weather forecast and the day-by-day activities concurrently.
            var weatherTask = GetWeatherAsync(trip.Destination, trip.StartDate, trip.EndDate);
            var dayPlans = await BuildDayPlansAsync(trip, numberOfDays, activitiesBudget);
            var weather = await weatherTask;

            AttachWeatherToDays(dayPlans, weather);

            await PersistPlanAsync(trip, hotelDto, flightDto, dayPlans, flightBudget, weather);

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
                Flight = flightDto is null ? null : MapFlight(flightDto, flightBudget),
                Days = dayPlans,
                Weather = weather,
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

            if (withinBudget != null)
                return withinBudget;

            
            return null;
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


        // ============================================================
        // Weather: fetch the destination forecast and map it to the plan
        // ============================================================
        private async Task<List<DayWeatherDto>> GetWeatherAsync(string city, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                var start = startDate.ToString("yyyy-MM-dd");
                var end = endDate.ToString("yyyy-MM-dd");

                // Don't let a slow/hanging weather API block the whole plan (5s cap).
                var task = _weatherPlugin.GetWeatherTimeline(city, start, end);
                var completed = await Task.WhenAny(task, Task.Delay(5000));
                if (completed != task)
                    return new List<DayWeatherDto>();

                var raw = await task;
                if (raw is not VisualCrossingResponseDto response)
                    return new List<DayWeatherDto>();

                return response.Days.Select(d => new DayWeatherDto
                {
                    Date = DateOnly.TryParse(d.Datetime, out var dt) ? dt : startDate,
                    TempMax = d.TempMax,
                    TempMin = d.TempMin,
                    Humidity = d.Humidity,
                    PrecipProb = d.PrecipProb,
                    Conditions = d.Conditions,
                    IconUrl = d.IconUrl
                }).ToList();
            }
            catch
            {
                return new List<DayWeatherDto>();
            }
        }

        private static void AttachWeatherToDays(List<DayPlanDto> dayPlans, List<DayWeatherDto> weather)
        {
            if (weather.Count == 0) return;

            var byDate = weather
                .GroupBy(w => w.Date)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var day in dayPlans)
            {
                if (byDate.TryGetValue(day.Date, out var w))
                    day.Weather = w;
            }
        }


        private async Task<List<DayPlanDto>> BuildDayPlansAsync(
            Trip trip, int numberOfDays, decimal activitiesBudget)
        {
            var dailyBudget = numberOfDays > 0 ? activitiesBudget / numberOfDays : 0;
            var usedPlaceIds = new HashSet<string>();
            var days = new List<DayPlanDto>();


            var attractionsTask = SearchPlaces(trip.Destination, "attraction");
            var restaurantsTask = SearchPlaces(trip.Destination, "restaurant");
            var cafesTask = SearchPlaces(trip.Destination, "cafe");

            await Task.WhenAll(attractionsTask, restaurantsTask, cafesTask);

            var attractions = attractionsTask.Result;
            var restaurants = restaurantsTask.Result;
            var cafes = cafesTask.Result;

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
                var task = _placesPlugin.SearchWithImages(city, category);
                var result = await Task.WhenAny(task, Task.Delay(5000));

                if (result != task)
                    return new List<PlaceDto>();

                return await task;
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
            var available = pool
                .Where(p => !usedPlaceIds.Contains(p.FsqPlaceId))
                .ToList();

            var place = available.Count == 0
                ? null
                : available[Random.Shared.Next(available.Count)];

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
                PlaceId = place.FsqPlaceId,
                Images = place.Images,
            });
        }

        private async Task PersistPlanAsync(
            Trip trip,
            GoogleHotelDto? hotel,
            FlightDto? flight,
            List<DayPlanDto> dayPlans,
            decimal flightBudget,
            List<DayWeatherDto> weather)
        {
            // Make rebuilds idempotent: wipe any previously-generated plan for this trip
            // (e.g. after a TRIP_UPDATE) so we don't accumulate duplicate hotels/flights/days.
            await ClearExistingPlanAsync(trip);

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
                    // The schedule provider returns no ticket price, so persist the
                    // orchestrator's allocated flight budget as the estimated cost.
                    Price = flightBudget,
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
                        PlaceId = activity.PlaceId,

                    });
                }

                await _unitOfWork.Repository<TripDay>().AddAsync(tripDay);
            }

            foreach (var w in weather)
            {
                await _unitOfWork.Repository<WeatherDay>().AddAsync(new WeatherDay
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    Date = w.Date,
                    TempMax = w.TempMax,
                    TempMin = w.TempMin,
                    Humidity = w.Humidity,
                    PrecipProb = w.PrecipProb,
                    Conditions = w.Conditions,
                    IconUrl = w.IconUrl
                });
            }

            trip.Status = TripStatus.Confirmed;
            await _unitOfWork.CompleteAsync();
        }

        // Deletes any previously-generated hotel/flight/day/activity rows for this trip
        // so a re-build (e.g. after the user updates the trip) doesn't create duplicates.
        private async Task ClearExistingPlanAsync(Trip trip)
        {
            var hadData = false;

            var activities = trip.Days.SelectMany(d => d.Activities).ToList();
            if (activities.Count > 0)
            {
                _unitOfWork.Repository<Activity>().DeleteRange(activities);
                hadData = true;
            }

            if (trip.Days.Count > 0)
            {
                _unitOfWork.Repository<TripDay>().DeleteRange(trip.Days);
                hadData = true;
            }

            if (trip.Hotels.Count > 0)
            {
                _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>().DeleteRange(trip.Hotels);
                hadData = true;
            }

            if (trip.Flights.Count > 0)
            {
                _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>().DeleteRange(trip.Flights);
                hadData = true;
            }

            if (trip.WeatherDays.Count > 0)
            {
                _unitOfWork.Repository<WeatherDay>().DeleteRange(trip.WeatherDays);
                hadData = true;
            }

            if (hadData)
            {
                await _unitOfWork.CompleteAsync();

                // Clear the in-memory navigation graph now that the rows are gone.
                trip.Days.Clear();
                trip.Hotels.Clear();
                trip.Flights.Clear();
                trip.WeatherDays.Clear();
            }
        }

        private static TripHotelDto MapHotel(GoogleHotelDto hotel) => new()
        {
            Name = hotel.Name,
            PricePerNight = hotel.Price.PricePerNight,
            Rating = hotel.Rating.Value,
            Address = hotel.Location.Address,
            Images = hotel.Images
        };

        private static TripFlightDto MapFlight(FlightDto flight, decimal estimatedPrice) => new()
        {
            AirlineName = flight.AirlineName,
            FlightNumber = flight.FlightNumber,
            DepartureAirport = flight.DepartureAirport,
            ArrivalAirport = flight.ArrivalAirport,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            EstimatedPrice = estimatedPrice
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

        //Update Trip
        public async Task<TripHotelDto?> RegenerateHotelAsync(Guid tripId)
        {
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");

            var currentHotel = trip.Hotels?.OrderByDescending(h => h.Id).FirstOrDefault();

            var checkIn = trip.StartDate.ToString("yyyy-MM-dd");
            var checkOut = trip.EndDate.ToString("yyyy-MM-dd");
            var numberOfNights = Math.Max(trip.EndDate.DayNumber - trip.StartDate.DayNumber, 1);
            var hotelBudget = trip.OriginCity is not null
                ? BudgetAllocator.HotelBudget(trip.BudgetTotal)
                : BudgetAllocator.WithoutFlight(trip.BudgetTotal).Item1;

            var maxPricePerNight = hotelBudget / numberOfNights;

            var filteredJson = await _hotelPlugin.FilterHotelsAsync(
                trip.Destination, checkIn, checkOut,
                maxPrice: maxPricePerNight,
                minRating: 3.5,
                amenities: new List<string>());

            var hotels = TryDeserialize<List<GoogleHotelDto>>(filteredJson) ?? new();

            if (hotels.Count <= 1)
            {
                var searchJson = await _hotelPlugin.SearchHotelsAsync(
                    trip.Destination, checkIn, checkOut, trip.NumTravelers, 0);
                hotels = TryDeserialize<List<GoogleHotelDto>>(searchJson) ?? new();
            }

            var withinBudget = hotels
      .Where(h => h.Price.PricePerNight.HasValue &&
                  h.Price.PricePerNight.Value <= (double)maxPricePerNight &&
                  (currentHotel == null ||
                   !string.Equals(h.Name, currentHotel.Name, StringComparison.OrdinalIgnoreCase)))
      .OrderByDescending(h => h.Rating.Value ?? 0)
      .ToList();

            var nextHotel = withinBudget.FirstOrDefault();
            if (nextHotel is null)
                return null;

            if (currentHotel is not null)
            {
               
                currentHotel.Name = nextHotel.Name;
                currentHotel.Address = nextHotel.Location.Address;
                currentHotel.Lat = nextHotel.Location.Latitude;
                currentHotel.Lng = nextHotel.Location.Longitude;
                currentHotel.PricePerNight = (decimal)(nextHotel.Price.PricePerNight ?? 0);
                currentHotel.Stars = (int)Math.Round(nextHotel.Rating.Value ?? 0);
                currentHotel.CheckIn = trip.StartDate;   
                currentHotel.CheckOut = trip.EndDate;    

                _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>().Update(currentHotel);
            }
            else
            {
                var hotelEntity = new SmartTravelPlaners.DAL.Entities.Hotel
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    Name = nextHotel.Name,
                    Address = nextHotel.Location.Address,
                    Lat = nextHotel.Location.Latitude,
                    Lng = nextHotel.Location.Longitude,
                    CheckIn = trip.StartDate,
                    CheckOut = trip.EndDate,
                    PricePerNight = (decimal)(nextHotel.Price.PricePerNight ?? 0),
                    Stars = (int)Math.Round(nextHotel.Rating.Value ?? 0),
                    Status = BookingStatus.Suggested
                };

                await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>().AddAsync(hotelEntity);
            }

            await _unitOfWork.CompleteAsync();

            return MapHotel(nextHotel);
        }

        public async Task<List<ActivityPlanDto>> RegenerateDayActivitiesAsync(Guid tripId, int dayNumber)
        {
           
            var tripDay = (await _unitOfWork.Repository<TripDay>()
                .FindAsync(d => d.TripId == tripId && d.DayNumber == dayNumber))
                .FirstOrDefault()
                ?? throw new Exception($"Day {dayNumber} not found for trip {tripId}");

           
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");

           
            var numberOfDays = Math.Max(trip.EndDate.DayNumber - trip.StartDate.DayNumber, 1);

            var activitiesBudget = trip.OriginCity is not null
                ? BudgetAllocator.ActivitiesBudget(trip.BudgetTotal)
                : BudgetAllocator.WithoutFlight(trip.BudgetTotal).Item2;

            var dailyBudget = activitiesBudget / numberOfDays;

           
            var oldActivities = await _unitOfWork.Repository<Activity>()
                .FindAsync(a => a.TripDayId == tripDay.Id);

            foreach (var act in oldActivities)
            {
                _unitOfWork.Repository<Activity>().Delete(act);
            }

            await _unitOfWork.CompleteAsync(); 

          
            var attractionsTask = SearchPlaces(trip.Destination, "attraction");
            var restaurantsTask = SearchPlaces(trip.Destination, "restaurant");
            var cafesTask = SearchPlaces(trip.Destination, "cafe");

            await Task.WhenAll(attractionsTask, restaurantsTask, cafesTask);

            
            var newActivities = new List<ActivityPlanDto>();

            var usedPlaceIds = new HashSet<string>(); 

            AddActivityIfAvailable(
                newActivities,
                attractionsTask.Result,
                usedPlaceIds,
                "Morning",
                "Attraction",
                dailyBudget * 0.4m);

            AddActivityIfAvailable(
                newActivities,
                restaurantsTask.Result,
                usedPlaceIds,
                "Lunch",
                "Restaurant",
                dailyBudget * 0.3m);

            AddActivityIfAvailable(
                newActivities,
                cafesTask.Result,
                usedPlaceIds,
                "Evening",
                "Cafe",
                dailyBudget * 0.3m);

            
            if (newActivities.Count == 0)
            {
                return new List<ActivityPlanDto>();
            }

           
            foreach (var activity in newActivities)
            {
                _unitOfWork.Repository<Activity>().AddAsync(new Activity
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

            await _unitOfWork.CompleteAsync();

            return newActivities;
        }
        public async Task SyncDayPlansAsync(Guid tripId)
        {
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");

            var numberOfDays = Math.Max(trip.EndDate.DayNumber - trip.StartDate.DayNumber, 1);

            var existingDays = trip.Days.OrderBy(d => d.DayNumber).ToList();
            var existingCount = existingDays.Count;

            if (existingCount == numberOfDays)
            {

                foreach (var day in existingDays)
                {
                    var expectedDate = trip.StartDate.AddDays(day.DayNumber - 1);
                    if (day.Date != expectedDate)
                    {
                        day.Date = expectedDate;
                        _unitOfWork.Repository<TripDay>().Update(day);
                    }
                }
            }
            else if (existingCount < numberOfDays)
            {

                var activitiesBudget = trip.OriginCity is not null
                    ? BudgetAllocator.ActivitiesBudget(trip.BudgetTotal)
                    : BudgetAllocator.WithoutFlight(trip.BudgetTotal).Item2;
                var dailyBudget = activitiesBudget / numberOfDays;

                var usedPlaceIds = existingDays
                    .SelectMany(d => d.Activities)
                    .Where(a => !string.IsNullOrEmpty(a.PlaceId))
                    .Select(a => a.PlaceId!)
                    .ToHashSet();

                var attractionsTask = SearchPlaces(trip.Destination, "attraction");
                var restaurantsTask = SearchPlaces(trip.Destination, "restaurant");
                var cafesTask = SearchPlaces(trip.Destination, "cafe");

                await Task.WhenAll(attractionsTask, restaurantsTask, cafesTask);

                var attractions = attractionsTask.Result;
                var restaurants = restaurantsTask.Result;
                var cafes = cafesTask.Result;

                for (int dayNumber = existingCount + 1; dayNumber <= numberOfDays; dayNumber++)
                {
                    var date = trip.StartDate.AddDays(dayNumber - 1);
                    var newActivities = new List<ActivityPlanDto>();

                    AddActivityIfAvailable(newActivities, attractions, usedPlaceIds, "Morning", "Attraction", dailyBudget * 0.4m);
                    AddActivityIfAvailable(newActivities, restaurants, usedPlaceIds, "Lunch", "Restaurant", dailyBudget * 0.3m);
                    AddActivityIfAvailable(newActivities, cafes, usedPlaceIds, "Evening", "Cafe", dailyBudget * 0.3m);

                    var tripDay = new TripDay
                    {
                        Id = Guid.NewGuid(),
                        TripId = trip.Id,
                        DayNumber = dayNumber,
                        Date = date,
                        BudgetAllocated = dailyBudget,
                        BudgetSpent = 0
                    };

                    foreach (var activity in newActivities)
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
                            PlaceId = activity.PlaceId,
                        });
                    }

                    await _unitOfWork.Repository<TripDay>().AddAsync(tripDay);
                }
            }
            else
            {

                var daysToRemove = existingDays
                    .Where(d => d.DayNumber > numberOfDays)
                    .ToList();

                foreach (var day in daysToRemove)
                {
                    foreach (var activity in day.Activities.ToList())
                    {
                        _unitOfWork.Repository<Activity>().Delete(activity);
                    }
                }
                await _unitOfWork.CompleteAsync();   

                foreach (var day in daysToRemove)
                {
                    _unitOfWork.Repository<TripDay>().Delete(day);
                }
                await _unitOfWork.CompleteAsync();
            }
            }
        public async Task<TripFlightDto?> RegenerateFlightAsync(Guid tripId)
        {
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");

            if (string.IsNullOrWhiteSpace(trip.OriginCity))
                return null; 

            var currentFlight = trip.Flights?.FirstOrDefault();
            var departureDate = trip.StartDate.ToString("yyyy-MM-dd");

            List<FlightDto> candidates;
            try
            {
                var json = await _flightPlugin.SearchFlightsAsync(
                    departureCity: trip.OriginCity,
                    arrivalCity: trip.Destination,
                    departureDate: departureDate,
                    tripType: "OneWay");

                var result = TryDeserialize<FlightSearchResult>(json);
                candidates = result?.OutboundFlights ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RegenerateFlightAsync search failed: {ex.Message}");
                return null;
            }

           
            var filtered = candidates
                .Where(f => currentFlight == null ||
                            !string.Equals(f.FlightNumber, currentFlight.FlightNumber, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var nextFlight = filtered.FirstOrDefault() ?? candidates.FirstOrDefault();
            if (nextFlight is null)
                return null;

            if (currentFlight is not null)
            {
                
                currentFlight.Airline = nextFlight.AirlineName;
                currentFlight.FlightNumber = nextFlight.FlightNumber;
                currentFlight.Origin = nextFlight.DepartureAirport;
                currentFlight.Destination = nextFlight.ArrivalAirport;
                currentFlight.DepartureTime = ParseDateTime(nextFlight.DepartureTime);
                currentFlight.ArrivalTime = ParseDateTime(nextFlight.ArrivalTime);
               

                _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>().Update(currentFlight);
            }
            else
            {
                var flightEntity = new SmartTravelPlaners.DAL.Entities.Flight
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    Airline = nextFlight.AirlineName,
                    FlightNumber = nextFlight.FlightNumber,
                    Origin = nextFlight.DepartureAirport,
                    Destination = nextFlight.ArrivalAirport,
                    DepartureTime = ParseDateTime(nextFlight.DepartureTime),
                    ArrivalTime = ParseDateTime(nextFlight.ArrivalTime),
                    Price = 0,
                    Status = BookingStatus.Suggested
                };

                await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>().AddAsync(flightEntity);
            }

            await _unitOfWork.CompleteAsync();

            return MapFlight(nextFlight,0);
        }

        public async Task<TripPlanDto> GetCurrentPlanAsync(Guid tripId)
        {
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                 ?? throw new Exception("الرحلة غير موجودة");

            var hotelEntity = (await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>()
                .FindAsync(h => h.TripId == tripId)).FirstOrDefault();

            var flightEntity = (await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>()
                .FindAsync(f => f.TripId == tripId)).FirstOrDefault();

            var tripDays = (await _unitOfWork.Repository<TripDay>()
                .FindAsync(d => d.TripId == tripId))
                .OrderBy(d => d.DayNumber)
                .ToList();

            var numberOfNights = Math.Max(trip.EndDate.DayNumber - trip.StartDate.DayNumber, 1);

            var dayDtos = tripDays.Select(d => new DayPlanDto
            {
                DayNumber = d.DayNumber,
                Date = d.Date,
                BudgetAllocated = d.BudgetAllocated,
                Activities = d.Activities.Select(a => new ActivityPlanDto
                {
                    Name = a.Name,
                    Type = a.Type.ToString(),
                    LocationName = a.LocationName,
                    Lat = a.Lat,
                    Lng = a.Lng,
                    TimeSlot = "Morning",
                    EstimatedCost = a.EstimatedCost,
                    PlaceId = a.PlaceId
                }).ToList()
            }).ToList();

            var estimatedTotal =
                (hotelEntity?.PricePerNight ?? 0) * numberOfNights
                + dayDtos.Sum(d => d.Activities.Sum(a => a.EstimatedCost));

            return new TripPlanDto
            {
                TripId = trip.Id,
                Destination = trip.Destination,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                BudgetTotal = trip.BudgetTotal,
                EstimatedTotalCost = estimatedTotal,
                Hotel = hotelEntity is null ? null : new TripHotelDto
                {
                    Name = hotelEntity.Name,
                    PricePerNight = (double)hotelEntity.PricePerNight,
                    Rating = hotelEntity.Stars,
                    Address = hotelEntity.Address
                },
                Flight = flightEntity is null ? null : new TripFlightDto
                {
                    AirlineName = flightEntity.Airline,
                    FlightNumber = flightEntity.FlightNumber,
                    DepartureAirport = flightEntity.Origin,
                    ArrivalAirport = flightEntity.Destination,
                    DepartureTime = flightEntity.DepartureTime.ToString("o"),
                    ArrivalTime = flightEntity.ArrivalTime.ToString("o")
                },
                Days = dayDtos,
                Summary = $"Trip to {trip.Destination} ({trip.StartDate} - {trip.EndDate}), " +
                          $"{(hotelEntity != null ? $"staying at {hotelEntity.Name}" : "no hotel selected")}" +
                          $"{(flightEntity != null ? $" with a flight via {flightEntity.Airline}" : "")}, " +
                          $"with {dayDtos.Count} day(s) of planned activities."
            };
        }
    }
}
