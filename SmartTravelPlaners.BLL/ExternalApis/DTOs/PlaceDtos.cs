using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.DTOs
{
   
        public class PlaceDto
        {
            public string FsqPlaceId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double? Rating { get; set; }
            public double? Popularity { get; set; }
            public int? Price { get; set; }
            public string? Website { get; set; }
        }

        public class FoursquareSearchResponse
        {
            public List<FoursquarePlace> Results { get; set; } = new();
        }

        public class FoursquarePlace
        {
            public string Fsq_Place_Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double? Rating { get; set; }
            public double? Popularity { get; set; }
            public int? Price { get; set; }
            public string? Website { get; set; }
            public FoursquareLocation? Location { get; set; }
            public List<FoursquareCategory> Categories { get; set; } = new();
        }

        public class FoursquareLocation
        {
            public string? Formatted_Address { get; set; }
        }

        public class FoursquareCategory
        {
            public string Name { get; set; } = string.Empty;
        }
    }
