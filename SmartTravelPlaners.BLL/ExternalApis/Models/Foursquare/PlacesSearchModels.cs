using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.Models.Foursquare
{
    public class FoursquareSearchResponse
    {
        public List<FoursquarePlace> results { get; set; } = new();
    }

    public class FoursquarePlace
    {
        [JsonPropertyName("fsq_place_id")]
        public string Fsq_Place_Id { get; set; } = string.Empty;
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public FoursquareLocation? Location { get; set; }
        public List<FoursquareCategory> Categories { get; set; } = new();
    }

    public class FoursquareLocation
    {
        public string? Formatted_Address { get; set; }
    }

    public class FoursquareCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
