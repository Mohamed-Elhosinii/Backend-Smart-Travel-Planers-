using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;
using SmartTravelPlaners.BLL.Features.Hotel.Settings;
using SmartTravelPlaners.BLL.Helpers;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Hotel.Services
{
    public class StayApiLookupResponse
    {
        [JsonPropertyName("data")]
        public List<StayApiDestResult> Data { get; set; } = new();
    }

    public class StayApiDestResult
    {
        [JsonPropertyName("dest_id")]
        public string DestId { get; set; } = string.Empty;

        [JsonPropertyName("dest_type")]
        public string DestType { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
    }

    public class PlaceResolverService : IPlaceResolverService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly HttpClient _httpClient;
        private readonly HotelApiSettings _settings;
        private readonly ILogger<PlaceResolverService> _logger;

        public PlaceResolverService(
            IUnitOfWork unitOfWork,
            HttpClient httpClient,
            IOptions<HotelApiSettings> settings,
            ILogger<PlaceResolverService> logger)
        {
            _unitOfWork = unitOfWork;
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            
            // Assuming base URL from settings is used, or override it specifically for Booking endpoints
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

        public async Task<PlaceResolutionResult> ResolveAsync(string userInput)
        {
            var normalized = FuzzyMatcher.Normalize(userInput);
            if (string.IsNullOrWhiteSpace(normalized))
                return PlaceResolutionResult.NotFound(userInput);

            // 1. Exact match from cache
            var exact = await _unitOfWork.DestinationCache.GetByNormalizedQueryAsync(normalized);
            if (exact != null)
            {
                await _unitOfWork.DestinationCache.UpdateLastUsedAtAsync(exact.Id);
                await _unitOfWork.CompleteAsync();
                return PlaceResolutionResult.Exact(exact.PlaceId, exact.Type ?? "city", exact.Name, "cache");
            }

            // 2. Fuzzy match against existing cache
            var candidates = await _unitOfWork.DestinationCache.GetAllForFuzzyMatchAsync();
            var closeMatch = FuzzyMatcher.FindClosest(normalized, candidates, maxDistance: 2);
            if (closeMatch != null)
            {
                return PlaceResolutionResult.NeedsConfirmation(userInput, closeMatch.Value.DestId, closeMatch.Value.ResolvedName);
            }

            // 3. Call API
            var url = $"v1/booking/destinations/lookup?query={Uri.EscapeDataString(normalized)}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<StayApiLookupResponse>(content, options);

                    if (result != null && result.Data != null && result.Data.Count > 0)
                    {
                        var best = result.Data[0];
                        var nameToUse = !string.IsNullOrEmpty(best.Name) ? best.Name : best.Label;

                        // Save to cache
                        var cacheEntry = new PlaceCache
                        {
                            PlaceId = best.DestId,
                            Name = nameToUse,
                            Type = best.DestType,
                            NormalizedQuery = normalized,
                            LastUsedAt = DateTime.UtcNow,
                            CachedAt = DateTime.UtcNow,
                            // Set defaults for required fields
                            Lat = 0,
                            Lng = 0
                        };
                        await _unitOfWork.DestinationCache.AddAsync(cacheEntry);
                        await _unitOfWork.CompleteAsync();

                        return PlaceResolutionResult.Exact(best.DestId, best.DestType, nameToUse, "api");
                    }
                }
                else
                {
                    _logger.LogWarning("StayAPI lookup failed with status code {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling StayAPI lookup for query {Query}", normalized);
            }

            return PlaceResolutionResult.NotFound(userInput);
        }

        public async Task<PlaceResolutionResult> ConfirmAsync(string destId, string resolvedName)
        {
            // The user confirmed a fuzzy match. We could update LastUsedAt here if we wanted.
            // For now just return resolved.
            return PlaceResolutionResult.Exact(destId, "city", resolvedName, "cache");
        }
    }
}
