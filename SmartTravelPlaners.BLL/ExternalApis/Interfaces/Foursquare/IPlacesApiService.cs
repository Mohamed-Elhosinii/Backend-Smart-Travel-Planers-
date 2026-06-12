using SmartTravelPlaners.BLL.ExternalApis.DTOs.Foursquare;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.Interfaces.Foursquare
{
    public interface IPlacesApiService
    {
       
        Task<List<PlaceDto>> SearchAsync(double? lat, double? lon, string city, string? query = null, int limit = 20);
      

        Task<PlaceDetailsDto?> GetPlaceDetailsAsync(string fsqPlaceId);


        Task<List<PlacePhotoDto>> GetImages(string placeName, string category, string? address);

       

        Task<List<NearbyPlaceDto>> GetNearbyPlacesAsync(double latitude, double longitude);
    }
}
