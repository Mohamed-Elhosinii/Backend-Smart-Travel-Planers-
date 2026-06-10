using SmartTravelPlaners.BLL.ExternalApis.DTOs.Foursquare;
using SmartTravelPlaners.BLL.ExternalApis.Models.Foursquare;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SmartTravelPlaners.BLL.Mappers.Foursquare
{
    public static class PlaceMapper
    {
        public static PlaceDto ToDto(this FoursquarePlace places)
        {
            return new PlaceDto
            {
                FsqPlaceId = places.Fsq_Place_Id,
                Name = places.Name,
                Category = places.Categories?.FirstOrDefault()?.Name ?? "General",
                Address = places.Location?.Formatted_Address ?? "",
                Latitude = places.Latitude,
                Longitude = places.Longitude
            };
        }
        public static NearbyPlaceDto ToNearbyDto(this GeotaggingCandidate candidate)
        {
            return new NearbyPlaceDto
            {
                FsqPlaceId = candidate.Fsq_Place_Id,
                Name = candidate.Name ?? "",
                Address = candidate.Location?.Formatted_Address ?? "",
                Latitude = candidate.Latitude,
                Longitude = candidate.Longitude,
                Category = candidate.Categories?.FirstOrDefault()?.Name ?? "General"
            };
        }
        public static PlaceDetailsDto ToDetailsDto(this FoursquarePlaceDetailsResponse p)
        {
            return new PlaceDetailsDto
            {
                FsqPlaceId = p.Fsq_Place_Id,
                Name = p.Name,
                Category = p.Categories?.FirstOrDefault()?.Name ?? "General",
                Address = p.Location?.Formatted_Address ?? "",
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Phone = p.tel
            };
        }
    }
}

