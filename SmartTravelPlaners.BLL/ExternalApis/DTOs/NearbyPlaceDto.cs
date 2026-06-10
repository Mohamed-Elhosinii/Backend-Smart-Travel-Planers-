using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.DTOs
{
    public class NearbyPlaceDto
    {
        public string FsqPlaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class GeotaggingResponse
    {
        public List<FoursquarePlace> Results { get; set; } = new();
    }
}