using System.Text.Json.Serialization;

namespace SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.DTOs
{
    public class VisualCrossingDayItem
    {
        [JsonPropertyName("datetime")]
        public string Datetime { get; set; } = string.Empty;

        [JsonPropertyName("tempmax")]
        public double TempMax { get; set; }

        [JsonPropertyName("tempmin")]
        public double TempMin { get; set; }

        [JsonPropertyName("humidity")]
        public double Humidity { get; set; }

        [JsonPropertyName("precipprob")]
        public double PrecipProb { get; set; }

        [JsonPropertyName("conditions")]
        public string Conditions { get; set; } = string.Empty;

        private string _icon = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                if (!string.IsNullOrEmpty(value))
                {
                    IconUrl = $"https://raw.githubusercontent.com/visualcrossing/WeatherIcons/main/SVG/1st%20Set%20-%20Color/{value}.svg";
                }
            }
        }

        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; } = string.Empty;
    }
}