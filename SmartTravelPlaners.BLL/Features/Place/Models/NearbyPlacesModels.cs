using SmartTravelPlaners.BLL.Features.Place.DTOs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.Features.Place.Models
{
    public class GeotaggingCandidate
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
    public class GeotaggingResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeotaggingCandidate> Candidates { get; set; } = new();
    }
}
