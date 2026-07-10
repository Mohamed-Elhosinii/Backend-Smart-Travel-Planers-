using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<TripOrchestratorService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TripOrchestratorService(
            IUnitOfWork unitOfWork,
            HotelPlugin hotelPlugin,
            FlightPlugin flightPlugin,
            PlacesPlugin placesPlugin,
            WeatherPlugin weatherPlugin,
            ILogger<TripOrchestratorService> logger)
        {
            _unitOfWork = unitOfWork;
            _hotelPlugin = hotelPlugin;

            _flightPlugin = flightPlugin;
            _placesPlugin = placesPlugin;
            _weatherPlugin = weatherPlugin;
            _logger = logger;
        }

        public async Task<TripPlanDto> BuildTripPlanAsync(Guid tripId)
        {
            try
            {
                _logger.LogInformation("Building trip plan. TripId: {TripId}", tripId);

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

                _logger.LogInformation("Budget allocated. Hotel: {HotelBudget}, Flight: {FlightBudget}, Activities: {ActivitiesBudget}", 
                    hotelBudget, flightBudget, activitiesBudget);

                var hotelDto = await SelectHotelAsync(trip, checkIn, checkOut, hotelBudget, numberOfNights);

                FlightDto? flightDto = null;

                if (hasOrigin)
                {
                    try
                    {
                        flightDto = await SelectFlightAsync(trip, checkIn);
                        _logger.LogInformation("Flight selected. Airline: {Airline}, FlightNumber: {FlightNumber}", 
                            flightDto?.AirlineName, flightDto?.FlightNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Flight selection failed for TripId: {TripId}, but continuing with plan", tripId);
                        flightDto = null;
                    }
                }

                // Fetch the weather forecast first so we can use it to plan days intelligently.
                var weatherResult = await GetWeatherAsync(trip.Destination, trip.StartDate, trip.EndDate);
                var weather = weatherResult.Weather;
                var dayPlans = await BuildDayPlansAsync(trip, numberOfDays, activitiesBudget, weather, weatherResult.Lat, weatherResult.Lng);

                AttachWeatherToDays(dayPlans, weather);

                _logger.LogInformation("Persisting plan data. DayCount: {DayCount}", dayPlans.Count);
                await PersistPlanAsync(trip, hotelDto, flightDto, dayPlans, flightBudget, weather);

                var estimatedTotal =
                    (decimal)(hotelDto?.Price.PricePerNight ?? 0) * Math.Max(numberOfNights, 1)
                    + (flightDto != null ? flightBudget : 0)
                    + dayPlans.Sum(d => d.Activities.Sum(a => a.EstimatedCost));

                // ConfirmedCost = only actual confirmed prices (hotel only for now)
                var confirmedCost = (decimal)(hotelDto?.Price.PricePerNight ?? 0) * Math.Max(numberOfNights, 1);

                // Validate budget and generate warnings
                var budgetWarnings = BudgetValidator.ValidateBudget(
                    trip.BudgetTotal,
                    trip.BudgetSpent,
                    confirmedCost,
                    estimatedTotal
                );

                // Add hotel-specific warning if needed
                var hotelWarning = BudgetValidator.ValidateHotelCost(
                    confirmedCost,
                    trip.BudgetTotal,
                    hasOrigin
                );
                if (hotelWarning != null)
                {
                    budgetWarnings.Add(hotelWarning);
                }

                var plan = new TripPlanDto
                {
                    TripId = trip.Id,
                    Destination = trip.Destination,
                    StartDate = trip.StartDate,
                    EndDate = trip.EndDate,
                    BudgetTotal = trip.BudgetTotal,
                    BudgetSpent = trip.BudgetSpent,
                    ConfirmedCost = confirmedCost,
                    EstimatedTotalCost = estimatedTotal,
                    Hotel = hotelDto is null ? null : MapHotel(hotelDto),
                    Flight = flightDto is null ? null : MapFlight(flightDto, flightBudget),
                    Days = dayPlans,
                    Weather = weather,
                    BudgetWarnings = budgetWarnings,
                    BudgetBreakdown = BuildBudgetBreakdown(
                        trip.BudgetTotal,
                        hasOrigin,
                        confirmedCost,
                        flightDto != null ? flightBudget : 0,
                        dayPlans.Sum(d => d.Activities.Sum(a => a.EstimatedCost))
                    ),
                    Summary = BuildSummary(trip, hotelDto, flightDto, dayPlans)
                };

                _logger.LogInformation("Trip plan built successfully. TripId: {TripId}, EstimatedCost: {EstimatedCost}", tripId, estimatedTotal);

                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build trip plan. TripId: {TripId}. Error: {ErrorMessage}", tripId, ex.Message);
                throw;
            }
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
                var tripTypePref = trip.Preferences?.FirstOrDefault(p => p.Category == "FlightType")?.Value ?? "OneWay";
                var returnDate = tripTypePref.Equals("RoundTrip", StringComparison.OrdinalIgnoreCase) ? trip.EndDate.ToString("yyyy-MM-dd") : null;

                var json = await _flightPlugin.SearchFlightsAsync(
                    departureCity: trip.OriginCity ?? "",
                    arrivalCity: trip.Destination,
                    departureDate: departureDate,
                    tripType: tripTypePref,
                    returnDate: returnDate);

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
        private async Task<(List<DayWeatherDto> Weather, double Lat, double Lng)> GetWeatherAsync(string city, DateOnly startDate, DateOnly endDate)
        {
            try
            {
                var start = startDate.ToString("yyyy-MM-dd");
                var end = endDate.ToString("yyyy-MM-dd");

                // Don't let a slow/hanging weather API block the whole plan (15s cap).
                var task = _weatherPlugin.GetWeatherTimeline(city, start, end);
                var completed = await Task.WhenAny(task, Task.Delay(15000));
                if (completed != task)
                    return (new List<DayWeatherDto>(), 0, 0);

                var rawJson = await task;
                var response = TryDeserialize<VisualCrossingResponseDto>(rawJson);
                
                if (response is null || response.Days is null)
                    return (new List<DayWeatherDto>(), 0, 0);

                var weatherList = response.Days.Select(d => new DayWeatherDto
                {
                    Date = DateOnly.TryParse(d.Datetime, out var dt) ? dt : startDate,
                    TempMax = d.TempMax,
                    TempMin = d.TempMin,
                    Conditions = d.Conditions,
                    IconUrl = d.IconUrl,
                    PrecipProb = d.PrecipProb
                }).OrderBy(w => w.Date).ToList();

                return (weatherList, response.Latitude, response.Longitude);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to map weather forecast for city: {City}", city);
                return (new List<DayWeatherDto>(), 0, 0);
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
            Trip trip, int numberOfDays, decimal activitiesBudget, List<DayWeatherDto> weatherList, double lat, double lng)
        {
            var dailyBudget = numberOfDays > 0 ? activitiesBudget / numberOfDays : 0;
            var usedPlaceIds = new HashSet<string>();
            var days = new List<DayPlanDto>();


            var baseCategories = new List<string> { "attraction", "restaurant", "cafe", "museum", "park", "shopping" };
            var userPrefs = new List<string>();
            var categories = baseCategories.ToList();

            if (trip.Preferences != null && trip.Preferences.Any())
            {
                userPrefs = trip.Preferences.Select(p => p.Value.ToLower()).Distinct().ToList();
                categories = categories.Union(userPrefs).ToList();
            }

            var categoryTasks = categories.ToDictionary(
                c => c, 
                c => SearchPlacesByCoords(lat, lng, c, trip.Destination)
            );

            await Task.WhenAll(categoryTasks.Values);

            var placesByCategory = categoryTasks.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Result
            );

            var random = new Random();
            var timeSlots = new[] { "Morning", "Afternoon", "Evening", "Night" };

            for (int dayNumber = 1; dayNumber <= numberOfDays; dayNumber++)
            {
                var date = trip.StartDate.AddDays(dayNumber - 1);
                var activities = new List<ActivityPlanDto>();

                // Dynamically decide how many activities today (2 to 4)
                int activitiesToday = random.Next(2, 5);
                var costPerActivity = activitiesToday > 0 ? dailyBudget / activitiesToday : 0;

                var currentDayWeather = weatherList.FirstOrDefault(w => w.Date == date);
                var isRainy = currentDayWeather != null && 
                              (currentDayWeather.PrecipProb > 30 || (currentDayWeather.Conditions ?? "").Contains("Rain", StringComparison.OrdinalIgnoreCase));

                var validCategories = categories.ToList();
                if (isRainy)
                {
                    validCategories.Remove("park");
                    // Can add more outdoor categories here if needed
                }

                var validPrefs = userPrefs.Where(p => validCategories.Contains(p)).ToList();
                var dailyCategories = new List<string>();

                // Prioritize user preferences (take up to 2, or 1 if day is short)
                if (validPrefs.Any())
                {
                    int prefsToTake = Math.Min(validPrefs.Count, activitiesToday > 2 ? 2 : 1);
                    var selectedPrefs = validPrefs.OrderBy(x => random.Next()).Take(prefsToTake).ToList();
                    dailyCategories.AddRange(selectedPrefs);
                    validCategories = validCategories.Except(selectedPrefs).ToList();
                }

                // Fill the rest with random categories
                int remaining = activitiesToday - dailyCategories.Count;
                if (remaining > 0)
                {
                    dailyCategories.AddRange(validCategories.OrderBy(x => random.Next()).Take(remaining));
                }

                // Shuffle the final list so preferences aren't always first in the day
                dailyCategories = dailyCategories.OrderBy(x => random.Next()).ToList();
                var dailySlots = timeSlots.OrderBy(x => random.Next()).Take(activitiesToday).OrderBy(x => Array.IndexOf(timeSlots, x)).ToList();

                for (int i = 0; i < activitiesToday; i++)
                {
                    var cat = dailyCategories[i];
                    var slot = dailySlots[i];
                    var pool = placesByCategory[cat];

                    AddActivityIfAvailable(activities, pool, usedPlaceIds, slot, char.ToUpper(cat[0]) + cat.Substring(1), costPerActivity);
                }

                days.Add(new DayPlanDto
                {
                    DayNumber = dayNumber,
                    Date = date,
                    BudgetAllocated = dailyBudget,
                    Activities = activities.OrderBy(a => Array.IndexOf(timeSlots, a.TimeSlot)).ToList()
                });
            }

            return days;
        }

        private async Task<List<PlaceDto>> SearchPlacesByCoords(double lat, double lng, string category, string city)
        {
            try
            {
                var task = _placesPlugin.SearchByCoordsAsync(lat, lng, category, city);
                var result = await Task.WhenAny(task, Task.Delay(15000));

                if (result != task)
                    return new List<PlaceDto>();

                var places = await task;
                return places ?? new List<PlaceDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch places by coords for city: {City}, Category: {Category}", city, category);
                return new List<PlaceDto>();
            }
        }

        private async Task<List<PlaceDto>> SearchPlaces(string city, string category)
        {
            try
            {
                var task = _placesPlugin.SearchWithImages(city, category);
                var result = await Task.WhenAny(task, Task.Delay(15000));

                if (result != task)
                    return new List<PlaceDto>();

                var rawJson = await task;
                var deserialized = TryDeserialize<List<PlaceDto>>(rawJson) ?? new List<PlaceDto>();
                return deserialized;
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
                    Status = BookingStatus.Suggested,
                    ImagesJson = hotel.Images != null ? System.Text.Json.JsonSerializer.Serialize(hotel.Images) : "[]"
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
                        TimeSlot = activity.TimeSlot ?? "Morning",
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

            // Trip.BudgetSpent = hotel cost + flight cost + all activities cost
            var numberOfNights = Math.Max(trip.EndDate.DayNumber - trip.StartDate.DayNumber, 1);
            var hotelCost   = hotel  is not null ? (decimal)(hotel.Price.PricePerNight ?? 0) * numberOfNights : 0;
            var flightCost  = flight is not null ? flightBudget : 0;
            var totalActivitiesCost = dayPlans.Sum(d => d.Activities.Sum(a => a.EstimatedCost));
            
            trip.BudgetSpent = hotelCost + flightCost + totalActivitiesCost;
            
            // Trip.ConfirmedCost = hotel cost only (actual confirmed price)
            trip.ConfirmedCost = hotelCost;
            
            trip.Status = TripStatus.Confirmed;
            _unitOfWork.Trips.Update(trip);

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "BudgetSpent persisted. TripId: {TripId}, Hotel: {Hotel}, Flight: {Flight}, Activities: {Activities}, Total: {Total}",
                trip.Id, hotelCost, flightCost, totalActivitiesCost, trip.BudgetSpent);
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

        private static BudgetBreakdownDto BuildBudgetBreakdown(
            decimal budgetTotal,
            bool hasOriginCity,
            decimal hotelActual,
            decimal flightActual,
            decimal activitiesActual)
        {
            decimal hotelAllocated, flightAllocated, activitiesAllocated;

            if (hasOriginCity)
            {
                hotelAllocated = BudgetAllocator.HotelBudget(budgetTotal);
                flightAllocated = BudgetAllocator.FlightBudget(budgetTotal);
                activitiesAllocated = BudgetAllocator.ActivitiesBudget(budgetTotal);
            }
            else
            {
                var (hb, ab) = BudgetAllocator.WithoutFlight(budgetTotal);
                hotelAllocated = hb;
                flightAllocated = 0;
                activitiesAllocated = ab;
            }

            return new BudgetBreakdownDto
            {
                Hotel = new BudgetCategoryDto
                {
                    Allocated = hotelAllocated,
                    Actual = hotelActual,
                    Status = "confirmed"
                },
                Flight = new BudgetCategoryDto
                {
                    Allocated = flightAllocated,
                    Actual = flightActual,
                    Status = hasOriginCity ? "estimated" : "not_applicable"
                },
                Activities = new BudgetCategoryDto
                {
                    Allocated = activitiesAllocated,
                    Actual = activitiesActual,
                    Status = "estimated"
                }
            };
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

            if (hotels.Count == 0)
            {
                var searchJson = await _hotelPlugin.SearchHotelsAsync(
                    trip.Destination, checkIn, checkOut, trip.NumTravelers, 0);
                var searchHotels = TryDeserialize<List<GoogleHotelDto>>(searchJson) ?? new();
                if (searchHotels.Count > 0)
                 {
                    hotels = searchHotels;
                }
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
                currentHotel.ImagesJson = nextHotel.Images != null ? System.Text.Json.JsonSerializer.Serialize(nextHotel.Images) : "[]";

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
                    Status = BookingStatus.Suggested,
                    ImagesJson = nextHotel.Images != null ? System.Text.Json.JsonSerializer.Serialize(nextHotel.Images) : "[]"
                };

                await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>().AddAsync(hotelEntity);
            }

            await _unitOfWork.CompleteAsync();

            // Recalculate Trip.BudgetSpent after hotel change
            var updatedTrip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");
            var nights = Math.Max(updatedTrip.EndDate.DayNumber - updatedTrip.StartDate.DayNumber, 1);
            var newHotelEntity = updatedTrip.Hotels.OrderByDescending(h => h.Id).FirstOrDefault();
            var flightEntity   = updatedTrip.Flights.FirstOrDefault();
            var allActivitiesCost = updatedTrip.Days.SelectMany(d => d.Activities).Sum(a => a.EstimatedCost);

            updatedTrip.BudgetSpent =
                (newHotelEntity?.PricePerNight ?? 0) * nights
                + (flightEntity?.Price ?? 0)
                + allActivitiesCost;

            // Recalculate Trip.ConfirmedCost after hotel change (hotel only)
            updatedTrip.ConfirmedCost = (newHotelEntity?.PricePerNight ?? 0) * nights;

            _unitOfWork.Trips.Update(updatedTrip);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "BudgetSpent recalculated after hotel regeneration. TripId: {TripId}, Total: {Total}",
                tripId, updatedTrip.BudgetSpent);

            return MapHotel(nextHotel);
        }

        public async Task<TripHotelDto?> SetSpecificHotelAsync(Guid tripId, string name, decimal pricePerNight, double lat, double lng, string address, double rating, string imagesJson)
        {
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");

            var oldHotels = await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>()
                .FindAsync(h => h.TripId == tripId);

            foreach (var h in oldHotels)
            {
                _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>().Delete(h);
            }

            var hotelEntity = new SmartTravelPlaners.DAL.Entities.Hotel
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                Name = name,
                Address = address,
                Lat = lat,
                Lng = lng,
                CheckIn = trip.StartDate,
                CheckOut = trip.EndDate,
                PricePerNight = pricePerNight,
                Stars = (int)Math.Round(rating),
                Status = SmartTravelPlaners.DAL.Enums.BookingStatus.Suggested,
                ImagesJson = string.IsNullOrWhiteSpace(imagesJson) ? "[]" : imagesJson
            };

            await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Hotel>().AddAsync(hotelEntity);
            await _unitOfWork.CompleteAsync();

            return new TripHotelDto
            {
                Name = name,
                Address = address,
                PricePerNight = (double)pricePerNight,
                Rating = rating,
                Images = string.IsNullOrWhiteSpace(imagesJson) || imagesJson == "[]" 
                         ? new List<string>() 
                         : System.Text.Json.JsonSerializer.Deserialize<List<string>>(imagesJson) ?? new List<string>()
            };
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

            var categories = new List<string> { "attraction", "restaurant", "cafe", "museum", "park", "shopping" };
            if (trip.Preferences != null && trip.Preferences.Any())
            {
                var prefs = trip.Preferences.Select(p => p.Value.ToLower()).ToList();
                categories = categories.Union(prefs).ToList();
            }

            var categoryTasks = categories.ToDictionary(
                c => c, 
                c => SearchPlaces(trip.Destination, c)
            );

            await Task.WhenAll(categoryTasks.Values);

            var placesByCategory = categoryTasks.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Result
            );

            var newActivities = new List<ActivityPlanDto>();
            var usedPlaceIds = new HashSet<string>();
            var random = new Random();
            var timeSlots = new[] { "Morning", "Afternoon", "Evening", "Night" };

            int activitiesToday = random.Next(2, 5);
            var costPerActivity = activitiesToday > 0 ? dailyBudget / activitiesToday : 0;

            var dailyCategories = categories.OrderBy(x => random.Next()).Take(activitiesToday).ToList();
            var dailySlots = timeSlots.OrderBy(x => random.Next()).Take(activitiesToday).OrderBy(x => Array.IndexOf(timeSlots, x)).ToList();

            for (int i = 0; i < activitiesToday; i++)
            {
                var cat = dailyCategories[i];
                var slot = dailySlots[i];
                var pool = placesByCategory[cat];

                AddActivityIfAvailable(newActivities, pool, usedPlaceIds, slot, char.ToUpper(cat[0]) + cat.Substring(1), costPerActivity);
            }

            newActivities = newActivities.OrderBy(a => Array.IndexOf(timeSlots, a.TimeSlot)).ToList();

            
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
                    TimeSlot = activity.TimeSlot ?? "Morning",
                    EstimatedCost = activity.EstimatedCost,
                    Status = ActivityStatus.Suggested,
                    PlaceId = activity.PlaceId
                });
            }

            await _unitOfWork.CompleteAsync();

            // Update TripDay.BudgetSpent for this day
            var daySpent = newActivities.Sum(a => a.EstimatedCost);
            tripDay.BudgetSpent = daySpent;
            _unitOfWork.Repository<TripDay>().Update(tripDay);
            await _unitOfWork.CompleteAsync();

            // Recalculate Trip.BudgetSpent across all days + hotel + flight
            var updatedTrip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");
            var nights = Math.Max(updatedTrip.EndDate.DayNumber - updatedTrip.StartDate.DayNumber, 1);
            var hotelEntity  = updatedTrip.Hotels.OrderByDescending(h => h.Id).FirstOrDefault();
            var flightEntity = updatedTrip.Flights.FirstOrDefault();
            var allActivitiesCost = updatedTrip.Days.SelectMany(d => d.Activities).Sum(a => a.EstimatedCost);

            updatedTrip.BudgetSpent =
                (hotelEntity?.PricePerNight ?? 0) * nights
                + (flightEntity?.Price ?? 0)
                + allActivitiesCost;

            _unitOfWork.Trips.Update(updatedTrip);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "BudgetSpent recalculated after day activities regeneration. TripId: {TripId}, DayNumber: {DayNumber}, DaySpent: {DaySpent}, TripTotal: {TripTotal}",
                tripId, dayNumber, daySpent, updatedTrip.BudgetSpent);

            return newActivities;
        }
        public async Task SyncDayPlansAsync(Guid tripId, string? changedField = null)
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
                await _unitOfWork.CompleteAsync();
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
                            TimeSlot = activity.TimeSlot ?? "Morning",
                            EstimatedCost = activity.EstimatedCost,
                            Status = ActivityStatus.Suggested,
                            PlaceId = activity.PlaceId,
                        });
                    }

                    await _unitOfWork.Repository<TripDay>().AddAsync(tripDay);
                    await _unitOfWork.CompleteAsync();

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
            {
                var oldFlights = await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>()
                    .FindAsync(f => f.TripId == tripId);
                foreach (var f in oldFlights)
                    _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>().Delete(f);
                await _unitOfWork.CompleteAsync();
                return null;
            }

            var currentFlight = trip.Flights?.FirstOrDefault();
            var departureDate = trip.StartDate.ToString("yyyy-MM-dd");

            var tripTypePref = trip.Preferences?.FirstOrDefault(p => p.Category == "FlightType")?.Value ?? "OneWay";
            var returnDate = tripTypePref.Equals("RoundTrip", StringComparison.OrdinalIgnoreCase) ? trip.EndDate.ToString("yyyy-MM-dd") : null;

            List<FlightDto> candidates;
            try
            {
                var json = await _flightPlugin.SearchFlightsAsync(
                    departureCity: trip.OriginCity,
                    arrivalCity: trip.Destination,
                    departureDate: departureDate,
                    tripType: tripTypePref,
                    returnDate: returnDate);

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

            var flightBudget = BudgetAllocator.FlightBudget(trip.BudgetTotal);
            var nextFlight = filtered.FirstOrDefault() ?? candidates.FirstOrDefault();

            
            var existingFlights = await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>()
                .FindAsync(f => f.TripId == tripId);
            foreach (var f in existingFlights)
                _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>().Delete(f);
            await _unitOfWork.CompleteAsync();

            if (nextFlight is null)
                return null;

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
                Price = flightBudget,
                Status = BookingStatus.Suggested
            };

            await _unitOfWork.Repository<SmartTravelPlaners.DAL.Entities.Flight>().AddAsync(flightEntity);
            await _unitOfWork.CompleteAsync();

            return MapFlight(nextFlight, flightBudget);
        }
        public async Task RegenerateWeatherAsync(Guid tripId)
        {
            var trip = await _unitOfWork.Trips.GetTripWithDetailsAsync(tripId)
                ?? throw new Exception($"Trip {tripId} not found");

            
            var oldWeather = await _unitOfWork.Repository<WeatherDay>()
                .FindAsync(w => w.TripId == tripId);
            foreach (var w in oldWeather)
                _unitOfWork.Repository<WeatherDay>().Delete(w);
            await _unitOfWork.CompleteAsync();

            
            var weatherResult = await GetWeatherAsync(trip.Destination, trip.StartDate, trip.EndDate);
            foreach (var w in weatherResult.Weather)
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
            await _unitOfWork.CompleteAsync();
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

            var weatherDays = (await _unitOfWork.Repository<WeatherDay>()
                .FindAsync(w => w.TripId == tripId))
                .Select(w => new DayWeatherDto
                {
                    Date = w.Date,
                    TempMax = w.TempMax,
                    TempMin = w.TempMin,
                    Humidity = w.Humidity,
                    PrecipProb = w.PrecipProb,
                    Conditions = w.Conditions,
                    IconUrl = w.IconUrl
                })
                .OrderBy(w => w.Date)
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
                    TimeSlot = a.TimeSlot,
                    EstimatedCost = a.EstimatedCost,
                    PlaceId = a.PlaceId
                }).ToList(),
                Weather = weatherDays.FirstOrDefault(w => w.Date == d.Date)
            }).ToList();

            var estimatedTotal =
     (hotelEntity?.PricePerNight ?? 0) * numberOfNights
     + (flightEntity?.Price ?? 0)
     + dayDtos.Sum(d => d.Activities.Sum(a => a.EstimatedCost));

            // ConfirmedCost = only actual confirmed prices (hotel only for now)
            var confirmedCost = (hotelEntity?.PricePerNight ?? 0) * numberOfNights;

            // Validate budget and generate warnings
            var budgetWarnings = BudgetValidator.ValidateBudget(
                trip.BudgetTotal,
                trip.BudgetSpent,
                confirmedCost,
                estimatedTotal
            );

            // Add hotel-specific warning if needed
            if (hotelEntity != null)
            {
                var hotelWarning = BudgetValidator.ValidateHotelCost(
                    confirmedCost,
                    trip.BudgetTotal,
                    !string.IsNullOrWhiteSpace(trip.OriginCity)
                );
                if (hotelWarning != null)
                {
                    budgetWarnings.Add(hotelWarning);
                }
            }

            return new TripPlanDto
            {
                TripId = trip.Id,
                Destination = trip.Destination,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                BudgetTotal = trip.BudgetTotal,
                BudgetSpent = trip.BudgetSpent,
                ConfirmedCost = confirmedCost,
                EstimatedTotalCost = estimatedTotal,
                Hotel = hotelEntity is null ? null : new TripHotelDto
                {
                    Name = hotelEntity.Name,
                    PricePerNight = (double)hotelEntity.PricePerNight,
                    Rating = hotelEntity.Stars,
                    Address = hotelEntity.Address,
                    Images = !string.IsNullOrEmpty(hotelEntity.ImagesJson) ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(hotelEntity.ImagesJson) ?? new List<string>() : new List<string>()
                },
                Flight = flightEntity is null ? null : new TripFlightDto
                {
                    AirlineName = flightEntity.Airline,
                    FlightNumber = flightEntity.FlightNumber,
                    DepartureAirport = flightEntity.Origin,
                    ArrivalAirport = flightEntity.Destination,
                    DepartureTime = flightEntity.DepartureTime.ToString("o"),
                    ArrivalTime = flightEntity.ArrivalTime.ToString("o"),
                    EstimatedPrice = flightEntity.Price
                },
                Days = dayDtos,
                Weather = weatherDays,
                BudgetWarnings = budgetWarnings,
                BudgetBreakdown = BuildBudgetBreakdown(
                    trip.BudgetTotal,
                    !string.IsNullOrWhiteSpace(trip.OriginCity),
                    confirmedCost,
                    flightEntity?.Price ?? 0,
                    dayDtos.Sum(d => d.Activities.Sum(a => a.EstimatedCost))
                ),
                Summary = $"Trip to {trip.Destination} ({trip.StartDate} - {trip.EndDate}), " +
                          $"{(hotelEntity != null ? $"staying at {hotelEntity.Name}" : "no hotel selected")}" +
                          $"{(flightEntity != null ? $" with a flight via {flightEntity.Airline}" : "")}, " +
                          $"with {dayDtos.Count} day(s) of planned activities."
            };
        }
    }
}
