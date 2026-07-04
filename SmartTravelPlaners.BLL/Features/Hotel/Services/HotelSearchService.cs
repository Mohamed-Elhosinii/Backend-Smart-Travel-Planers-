using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;
using SmartTravelPlaners.BLL.Features.Hotel.Settings;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Hotel.Services
{
    public class HotelSearchService : IHotelSearchService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly HttpClient _httpClient;
        private readonly HotelApiSettings _settings;
        private readonly ILogger<HotelSearchService> _logger;

        public HotelSearchService(
            IUnitOfWork unitOfWork,
            HttpClient httpClient,
            IOptions<HotelApiSettings> settings,
            ILogger<HotelSearchService> logger)
        {
            _unitOfWork = unitOfWork;
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            
            if (!_settings.BaseUrl.Contains("stayapi.com")) 
            {
                 _httpClient.BaseAddress = new Uri("https://api.stayapi.com/");
            }
            else 
            {
                 _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            }
            
            if (!_httpClient.DefaultRequestHeaders.Contains("X-API-Key"))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
            }
        }

        public async Task<HotelSearchResponseDto> SearchAsync(string destId, string destType, DateTime checkIn, DateTime checkOut, int adults = 2, int rooms = 1)
        {
            if (checkIn == default || checkOut == default)
                throw new ArgumentException("CheckIn and CheckOut dates are required.");

            int weekOfYear = ISOWeek.GetWeekOfYear(checkIn);
            int year = checkIn.Year;
            string cacheKey = $"hotels:{destId}:{year}-W{weekOfYear}:{adults}:{rooms}";

            var cachedData = await _unitOfWork.ExternalApiCache.GetAsync(cacheKey, "StayAPI_Search");
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    var cachedHotels = JsonSerializer.Deserialize<List<HotelDto>>(cachedData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cachedHotels != null && cachedHotels.Count > 0)
                    {
                        return new HotelSearchResponseDto
                        {
                            CacheHit = true,
                            Hotels = cachedHotels,
                            Metadata = new HotelSearchResponseDto.MetadataDto
                            {
                                TotalResults = cachedHotels.Count,
                                SourcesUsed = new List<string> { "StayAPI_Cache" },
                                RetrievedAt = DateTime.UtcNow
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize cached hotel search data.");
                }
            }

            var typeParam = string.IsNullOrEmpty(destType) ? "" : $"&dest_type={Uri.EscapeDataString(destType)}";
            var url = $"v1/booking/search?dest_id={Uri.EscapeDataString(destId)}{typeParam}&checkin={checkIn:yyyy-MM-dd}&checkout={checkOut:yyyy-MM-dd}&adults={adults}&rooms={rooms}";

            var hotels = new List<HotelDto>();

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var rawData = JsonSerializer.Deserialize<JsonElement>(content);

                    // Map StayAPI / Booking.com response to HotelDto
                    // Assuming structure based on standard StayAPI Booking search response
                    if (rawData.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in dataElement.EnumerateArray())
                        {
                            var dto = new HotelDto
                            {
                                HotelId = GetString(item, "hotel_id") ?? Guid.NewGuid().ToString(),
                                Name = GetString(item, "hotel_name") ?? "Unknown Hotel",
                                Stars = (int)(GetDouble(item, "class") ?? 0),
                                Sources = new List<string> { "BookingCom" }
                            };

                            var lat = GetDouble(item, "latitude") ?? 0;
                            var lng = GetDouble(item, "longitude") ?? 0;
                            var address = GetString(item, "address") ?? "";
                            
                            dto.Location = new LocationDto { Latitude = lat, Longitude = lng, Address = address };

                            var price = GetDouble(item, "price_breakdown", "gross_price", "value");
                            var currency = GetString(item, "price_breakdown", "gross_price", "currency");
                            
                            if (price.HasValue)
                            {
                                dto.Price = new HotelPriceDto
                                {
                                    Amount = (decimal)price.Value,
                                    Currency = currency ?? "USD",
                                    IsEstimated = false
                                };
                            }

                            var reviewScore = GetDouble(item, "review_score");
                            var reviewCount = (int)(GetDouble(item, "review_nr") ?? 0);

                            if (reviewScore.HasValue)
                            {
                                dto.Rating = new HotelReviewDto
                                {
                                    Score = reviewScore.Value,
                                    ReviewCount = reviewCount,
                                    WeightedScore = reviewScore.Value * Math.Log10(Math.Max(reviewCount, 1))
                                };
                            }

                            var imgUrl = GetString(item, "main_photo_url");
                            if (!string.IsNullOrEmpty(imgUrl))
                            {
                                dto.Images.Add(imgUrl.Replace("max1280x900", "max500")); // optimization
                            }

                            hotels.Add(dto);
                        }
                    }

                    if (hotels.Count > 0)
                    {
                        hotels = hotels.OrderByDescending(h => h.Rating?.WeightedScore ?? 0).ToList();
                        await _unitOfWork.ExternalApiCache.SetAsync(cacheKey, "StayAPI_Search", JsonSerializer.Serialize(hotels), TimeSpan.FromDays(7));
                        await _unitOfWork.CompleteAsync();
                    }
                }
                else
                {
                    _logger.LogWarning("StayAPI search failed with status {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from StayAPI Search.");
            }

            return new HotelSearchResponseDto
            {
                CacheHit = false,
                Hotels = hotels,
                Metadata = new HotelSearchResponseDto.MetadataDto
                {
                    TotalResults = hotels.Count,
                    SourcesUsed = new List<string> { "BookingCom" },
                    RetrievedAt = DateTime.UtcNow
                }
            };
        }

        private string? GetString(JsonElement element, params string[] properties)
        {
            var current = element;
            foreach (var prop in properties)
            {
                if (!current.TryGetProperty(prop, out current))
                    return null;
            }
            if (current.ValueKind == JsonValueKind.String)
                return current.GetString();
            if (current.ValueKind == JsonValueKind.Number)
                return current.GetRawText();
            return null;
        }

        private double? GetDouble(JsonElement element, params string[] properties)
        {
            var current = element;
            foreach (var prop in properties)
            {
                if (!current.TryGetProperty(prop, out current))
                    return null;
            }
            if (current.ValueKind == JsonValueKind.Number && current.TryGetDouble(out var result))
                return result;
            return null;
        }
    }
}
