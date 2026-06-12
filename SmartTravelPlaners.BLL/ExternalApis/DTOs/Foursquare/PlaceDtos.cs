using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.DTOs.Foursquare
{


    public class PlaceDto
        {
        public string FsqPlaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<PlacePhotoDto> Images { get; set; }


    }

    
}
