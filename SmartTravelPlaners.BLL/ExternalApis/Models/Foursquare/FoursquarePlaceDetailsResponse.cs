using SmartTravelPlaners.BLL.ExternalApis.DTOs.Foursquare;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.Models.Foursquare
{
    public class FoursquarePlaceDetailsResponse
    {
        public string Fsq_Place_Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<FoursquareCategory> Categories { get; set; } = new();
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? tel { get; set; }

        public FoursquareLocation? Location { get; set; }

        //public Dictionary<string, object>? Attributes { get; set; }
    }
}
