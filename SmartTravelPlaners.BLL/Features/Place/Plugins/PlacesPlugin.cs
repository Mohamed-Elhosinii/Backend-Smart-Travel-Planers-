using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SmartTravelPlaners.BLL.Features.Place.DTOs;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

    namespace SmartTravelPlaners.BLL.Features.Place.Plugins
    {
        public class PlacesPlugin
        {
            private readonly IPlacesApiService _service;


            public PlacesPlugin(IPlacesApiService service)
            {
                _service = service;

            }


            //places with image
            [KernelFunction]
            [Description("Search for places in a city and return enriched results including images. Use this when the user asks for places, restaurants, cafes, or attractions.Category is optional; if not provided, return a general search across all types.")]

            public async Task<List<PlaceDto>> SearchWithImages(
                [Description("Name of the city to search in, e.g. Cairo, Paris")] string city,
                [Description("Optional category filter like restaurant, cafe, museum, park. If null, return all types.")] string? category)
            {
                if (string.IsNullOrWhiteSpace(city))
                    return new List<PlaceDto>();


                var places = await _service.SearchAsync(city, category);

                if (places == null)
                    return new List<PlaceDto>();

                var tasks = places.Select(async place =>
                {
                    var images = await _service.GetImages(place.Name, place.Category ?? category ?? "", place.Address);

                    return new PlaceDto
                    {
                        FsqPlaceId = place.FsqPlaceId,  
                        Name = place.Name,
                        Address = place.Address,
                        Category = place.Category,
                        Latitude = place.Latitude,       
                        Longitude = place.Longitude,     
                        Images = images
                    };
                });

                var result = await Task.WhenAll(tasks);
                return result.ToList();
            }

            //Place details
            [KernelFunction]
            [Description("Get detailed information about a specific place using its unique place identifier from search results")]
            public Task<PlaceDetailsDto?> Details([Description("Unique identifier of the place returned from search results")] string id)
                => _service.GetPlaceDetailsAsync(id);

            //Nearby 
            [KernelFunction]
            [Description("Get nearby places based on geographic coordinates. Use this when the user asks for places near a specific location.")]
            public async Task<List<NearbyPlaceDto>> Nearby(
        [Description("Latitude coordinate  of the location")] double lat,

        [Description("Longitude coordinate  of the location")] double lon)
            {
                return await _service.GetNearbyPlacesAsync(lat, lon);
            }

            //Images
            [KernelFunction]
            [Description("Get a gallery of images for a specific place. Use this when the user asks to see photos of a place or wants visual details.")]
            public async Task<List<PlacePhotoDto>> Images(
        [Description("Name of the place")] string name,

        [Description("Category of the place such as restaurant, cafe, or tourism")] string category,

        [Description("Optional address or location to improve search accuracy")] string? address)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return new List<PlacePhotoDto>();

                return await _service.GetImages(name, category, address);
            }
        }
}
