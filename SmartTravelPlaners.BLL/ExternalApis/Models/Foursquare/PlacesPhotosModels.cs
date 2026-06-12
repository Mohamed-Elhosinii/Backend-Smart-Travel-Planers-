using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.ExternalApis.Models.Foursquare
{
    public class SerperResponse
    {
        public List<SerperImage> Images { get; set; } = new();
    }
    public class SerperImage
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }
}
