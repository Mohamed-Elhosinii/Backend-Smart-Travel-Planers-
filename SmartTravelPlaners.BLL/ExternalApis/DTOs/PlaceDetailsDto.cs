using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.DTOs
{
    public class PlaceDetailsDto
    {
        public string FsqPlaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Website { get; set; }
        public string? Description { get; set; }
        public double? Rating { get; set; }
        public int? Price { get; set; }

        public List<string> Features { get; set; } = new();
    }

    public class FoursquarePlaceDetailsResponse
    {
        public string Fsq_Place_Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Website { get; set; }
        public string? Description { get; set; }
        public double? Rating { get; set; }
        public int? Price { get; set; }

        public FoursquareLocation? Location { get; set; }

        public Dictionary<string, object>? Attributes { get; set; }
    }
}
