using SmartTravelPlaners.BLL.ExternalApis.Foursquare.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.FourSquare.Interfaces
{
    public interface IPlacesApiService
    {
       

        Task<List<PlaceDto>> SearchAsync( string city, string? query = null, int limit = 20);
      


        Task<PlaceDetailsDto?> GetPlaceDetailsAsync(string fsqPlaceId);


        Task<List<PlacePhotoDto>> GetImages(string placeName, string category, string? address);

       

        Task<List<NearbyPlaceDto>> GetNearbyPlacesAsync(double latitude, double longitude);
    }
}
