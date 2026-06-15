using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Place.Settings
{
    public class FoursquareSettings
    {
            public string ServiceKey { get; set; } = string.Empty;
            public string PlacesBaseUrl { get; set; } = string.Empty;
            public string PlacesVersion { get; set; } = string.Empty;
    }
}
