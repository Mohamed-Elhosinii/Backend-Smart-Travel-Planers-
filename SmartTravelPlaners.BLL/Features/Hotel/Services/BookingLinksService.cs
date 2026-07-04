using System;
using System.Collections.Generic;
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
    public class BookingLinksService : IBookingLinksService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly HttpClient _httpClient;
        private readonly HotelApiSettings _settings;
        private readonly ILogger<BookingLinksService> _logger;

        public BookingLinksService(
            IUnitOfWork unitOfWork,
            HttpClient httpClient,
            IOptions<HotelApiSettings> settings,
            ILogger<BookingLinksService> logger)
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

        public async Task<BookingLinksDto> GetLinksAsync(string hotelName, string? location = null)
        {
            string cacheKey = $"links:{hotelName}:{location ?? ""}";

            var cachedData = await _unitOfWork.ExternalApiCache.GetAsync(cacheKey, "StayAPI_Meta");
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    var cachedLinks = JsonSerializer.Deserialize<Dictionary<string, string>>(cachedData);
                    if (cachedLinks != null && cachedLinks.Count > 0)
                    {
                        return new BookingLinksDto { CacheHit = true, Links = cachedLinks };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize cached booking links for {HotelName}", hotelName);
                }
            }

            var links = new Dictionary<string, string>();
            var url = $"v1/meta/search?hotel_name={Uri.EscapeDataString(hotelName)}";
            if (!string.IsNullOrEmpty(location))
            {
                url += $"&location={Uri.EscapeDataString(location)}";
            }

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var rawData = JsonSerializer.Deserialize<JsonElement>(content);

                    // Typical StayAPI Meta search format might be an array of links or a dictionary
                    // We extract whatever is available
                    if (rawData.TryGetProperty("data", out var dataNode))
                    {
                        if (dataNode.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in dataNode.EnumerateArray())
                            {
                                var source = GetString(item, "source") ?? "Unknown";
                                var linkUrl = GetString(item, "url") ?? GetString(item, "link");
                                if (!string.IsNullOrEmpty(linkUrl) && !links.ContainsKey(source))
                                {
                                    links[source] = linkUrl;
                                }
                            }
                        }
                        else if (dataNode.ValueKind == JsonValueKind.Object)
                        {
                             foreach (var prop in dataNode.EnumerateObject())
                             {
                                 if (prop.Value.ValueKind == JsonValueKind.String)
                                 {
                                     links[prop.Name] = prop.Value.GetString()!;
                                 }
                             }
                        }
                    }

                    if (links.Count > 0)
                    {
                        // Cache for 30 days
                        await _unitOfWork.ExternalApiCache.SetAsync(cacheKey, "StayAPI_Meta", JsonSerializer.Serialize(links), TimeSpan.FromDays(30));
                        await _unitOfWork.CompleteAsync();
                    }
                }
                else
                {
                    _logger.LogWarning("StayAPI metasearch failed for {HotelName} with status {Status}", hotelName, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from StayAPI metasearch for {HotelName}", hotelName);
            }

            return new BookingLinksDto { CacheHit = false, Links = links };
        }

        private string? GetString(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var node) && node.ValueKind == JsonValueKind.String)
                return node.GetString();
            return null;
        }
    }
}
