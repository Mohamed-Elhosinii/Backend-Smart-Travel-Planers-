using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;

namespace SmartTravelPlaners.BLL.Features.Place.Plugins
{
    public class PlacesPlugin
    {
        private readonly IPlacesApiService _service;
        private readonly ILogger<PlacesPlugin> _logger;
        private static readonly SemaphoreSlim _serperSemaphore = new SemaphoreSlim(3);

        public PlacesPlugin(IPlacesApiService service, ILogger<PlacesPlugin> logger)
        {
            _service = service;
            _logger = logger;
        }

        // Search for places via Foursquare only — images are NOT fetched here.
        // Serper image enrichment was intentionally removed from this path because
        // it blocked the entire pipeline past the SearchPlaces 5s timeout, causing
        // all activity pools to come back empty and no Activities to be persisted.
        // Images can be fetched on-demand by the frontend via the /Places/images endpoint.
        [KernelFunction]
        [Description("Search for places in a city and return enriched results including images. Use this when the user asks for places, restaurants, cafes, or attractions.Category is optional; if not provided, return a general search across all types.")]
        public async Task<string> SearchWithImages(
            [Description("Name of the city to search in, e.g. Cairo, Paris")] string city,
            [Description("Optional category filter like restaurant, cafe, museum, park. If null, return all types.")] string? category)
        {
            if (string.IsNullOrWhiteSpace(city))
                return JsonSerializer.Serialize(new List<PlaceDto>());

            var places = await _service.SearchAsync(city, category);

            if (places == null || places.Count == 0)
                return JsonSerializer.Serialize(new List<PlaceDto>());

            // Return places immediately — no Serper call, no blocking.
            // Images field is left empty; the frontend fetches them lazily.
            var result = places.Select(p => new PlaceDto
            {
                FsqPlaceId = p.FsqPlaceId,
                Name = p.Name,
                Address = p.Address,
                Category = p.Category,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Images = new List<PlacePhotoDto>()
            }).ToList();

            _logger.LogInformation("SearchWithImages city={City} category={Category} returned {Count} places (no images)", city, category, result.Count);
            return JsonSerializer.Serialize(result);
        }

        //Place details
        [KernelFunction]
        [Description("Get detailed information about a specific place using its unique place identifier from search results")]
        public async Task<string> Details([Description("Unique identifier of the place returned from search results")] string id)
        {
            var details = await _service.GetPlaceDetailsAsync(id);
            return JsonSerializer.Serialize(details);
        }

        //Nearby 
        [KernelFunction]
        [Description("Get nearby places based on geographic coordinates. Use this when the user asks for places near a specific location.")]
        public async Task<string> Nearby(
            [Description("Latitude coordinate  of the location")] double lat,
            [Description("Longitude coordinate  of the location")] double lon)
        {
            var nearbyPlaces = await _service.GetNearbyPlacesAsync(lat, lon);
            return JsonSerializer.Serialize(nearbyPlaces);
        }

        //Images
        [KernelFunction]
        [Description("Get a gallery of images for a specific place. Use this when the user asks to see photos of a place or wants visual details.")]
        public async Task<string> Images(
            [Description("Name of the place")] string name,
            [Description("Category of the place such as restaurant, cafe, or tourism")] string category,
            [Description("Optional address or location to improve search accuracy")] string? address)
        {
            if (string.IsNullOrWhiteSpace(name))
                return JsonSerializer.Serialize(new List<PlacePhotoDto>());

            var images = await _service.GetImages(name, category, address);
            return JsonSerializer.Serialize(images);
        }
    }
}
