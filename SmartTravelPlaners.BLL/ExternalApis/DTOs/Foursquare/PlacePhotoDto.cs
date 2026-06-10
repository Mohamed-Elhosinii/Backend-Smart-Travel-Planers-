using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.DTOs.Foursquare
{
    public class PlacePhotoDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class FoursquarePhoto
    {
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }
}