using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;
using SmartTravelPlaners.BLL.Features.Hotel.Settings;
using System.Net.Http.Json;
using System.Text.Json;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Hotel.Services
{
    public class HotelApiService : IHotelApiService
    {
        private readonly HttpClient _httpClient;
        private readonly HotelApiSettings _settings;
        private readonly ILogger<HotelApiService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public HotelApiService(
            HttpClient httpClient,
            IOptions<HotelApiSettings> settings,
            ILogger<HotelApiService> logger,
            IUnitOfWork unitOfWork)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _unitOfWork = unitOfWork;

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
        }

        public async Task<List<GoogleHotelDto>> GetAvailableHotelsAsync(
            string location, string checkIn, string checkOut, int adults = 2, int children = 0)
        {
            if (!string.IsNullOrEmpty(location))
            {
                location = location.Trim();
                if (location.Contains(","))
                {
                    location = location.Split(',')[0].Trim();
                }
            }

            var cacheKey = $"hotels_search:{location}:{checkIn}:{checkOut}:{adults}:{children}";
            var cachedData = await _unitOfWork.ExternalApiCache.GetAsync(cacheKey, "StayAPI_Hotels");
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    var cachedHotels = JsonSerializer.Deserialize<List<GoogleHotelDto>>(cachedData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cachedHotels != null)
                    {
                        return cachedHotels;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize cached hotel search data.");
                }
            }

            var searchUrl = $"v1/google_hotels/search?location={Uri.EscapeDataString(location)}" +
                            $"&check_in={checkIn}&check_out={checkOut}" +
                            $"&adults={adults}&children={children}&currency=USD";

            _logger.LogInformation("StayAPI Request URL: {BaseAddress}{SearchUrl}", _httpClient.BaseAddress, searchUrl);

            try
            {
                var response = await _httpClient.GetAsync(searchUrl);
                var responseString = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("StayAPI raw response: {RawJson}", responseString);

                _logger.LogInformation("StayAPI Status Code: {StatusCode}", (int)response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("StayAPI returned non-success status: {StatusCode}", response.StatusCode);
                    return new List<GoogleHotelDto>();
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var searchResult = JsonSerializer.Deserialize<StayApiSearchResponse>(responseString, options);

                var hotels = searchResult?.Hotels ?? new List<GoogleHotelDto>();

                _logger.LogInformation("Deserialized hotels count: {Count}", hotels.Count);

                if (hotels.Count > 0)
                {
                    await _unitOfWork.ExternalApiCache.SetAsync(cacheKey, "StayAPI_Hotels", JsonSerializer.Serialize(hotels), TimeSpan.FromDays(7));
                    await _unitOfWork.CompleteAsync();
                }

                return hotels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while calling StayAPI with URL: {SearchUrl}", searchUrl);
                return new List<GoogleHotelDto>();
            }
        }

        public async Task<GoogleHotelDto?> GetHotelByIdAsync(
            string location, string checkIn, string checkOut, string hotelId)
        {
            var hotels = await GetAvailableHotelsAsync(location, checkIn, checkOut);

            var match = hotels.FirstOrDefault(h =>
                (!string.IsNullOrEmpty(h.HotelId) && h.HotelId == hotelId) ||
                string.Equals(h.Name, hotelId, StringComparison.OrdinalIgnoreCase));

            if (match is null)
                _logger.LogWarning("Hotel with id/name '{HotelId}' not found in {Location}", hotelId, location);

            return match;
        }

        public async Task<List<GoogleHotelDto>> FilterHotelsAsync(
            string location, string checkIn, string checkOut,
            decimal? maxPrice, decimal? minPrice, double? minRating, List<string>? amenities,
            int adults = 2, int children = 0)
        {
            var hotels = await GetAvailableHotelsAsync(location, checkIn, checkOut, adults, children);

            var filtered = hotels.AsEnumerable();

            if (maxPrice.HasValue)
            {
                filtered = filtered.Where(h =>
                    h.Price.PricePerNight.HasValue &&
                    h.Price.PricePerNight.Value <= (double)maxPrice.Value);
            }

            if (minPrice.HasValue)
            {
                filtered = filtered.Where(h =>
                    h.Price.PricePerNight.HasValue &&
                    h.Price.PricePerNight.Value >= (double)minPrice.Value);
            }

            if (minRating.HasValue)
            {
                filtered = filtered.Where(h =>
                    h.Rating.Value.HasValue &&
                    h.Rating.Value.Value >= minRating.Value);
            }

            if (amenities is { Count: > 0 })
            {
                filtered = filtered.Where(h =>
                    amenities.All(req =>
                        h.Amenities.Any(a => a.Contains(req, StringComparison.OrdinalIgnoreCase))));
            }

            var result = filtered
                .OrderBy(h => h.Price.PricePerNight ?? double.MaxValue)
                .ToList();

            return result;
        }

        public async Task<List<GoogleHotelDto>> GetHotelsNearLocationAsync(
            string location, string checkIn, string checkOut,
            double latitude, double longitude, int radiusKm,
            int adults = 2, int children = 0)
        {
            var hotels = await GetAvailableHotelsAsync(location, checkIn, checkOut, adults, children);

            return hotels
                .Where(h => h.Location.Latitude != 0 && h.Location.Longitude != 0 && CalculateDistanceKm(latitude, longitude, h.Location.Latitude, h.Location.Longitude) <= radiusKm)
                .OrderBy(h => CalculateDistanceKm(latitude, longitude, h.Location.Latitude, h.Location.Longitude))
                .ToList();
        }

        public async Task<List<GoogleHotelDto>> GetSimilarHotelsAsync(
            string location, string checkIn, string checkOut,
            string hotelId, int adults = 2, int children = 0)
        {
            var hotels = await GetAvailableHotelsAsync(location, checkIn, checkOut, adults, children);

            var target = hotels.FirstOrDefault(h =>
                (!string.IsNullOrEmpty(h.HotelId) && h.HotelId == hotelId) ||
                string.Equals(h.Name, hotelId, StringComparison.OrdinalIgnoreCase));

            if (target is null || !target.Price.PricePerNight.HasValue)
                return new List<GoogleHotelDto>();

            var targetPrice = target.Price.PricePerNight.Value;
            var targetRating = target.Rating.Value ?? 0;
            var priceRange = targetPrice * 0.3;

            return hotels
                .Where(h => h != target &&
                            h.Price.PricePerNight.HasValue &&
                            Math.Abs(h.Price.PricePerNight.Value - targetPrice) <= priceRange &&
                            Math.Abs((h.Rating.Value ?? 0) - targetRating) <= 1.0)
                .OrderByDescending(h => h.Rating.Value ?? 0)
                .Take(5)
                .ToList();
        }

        public async Task<bool> CheckAvailabilityAsync(
            string location, string checkIn, string checkOut, string hotelId)
        {
            var hotel = await GetHotelByIdAsync(location, checkIn, checkOut, hotelId);
            return hotel is not null;
        }

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    }
}