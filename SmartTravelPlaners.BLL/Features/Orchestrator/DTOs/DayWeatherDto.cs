using System;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    /// <summary>
    /// One day of weather forecast for the trip destination,
    /// mapped from the Visual Crossing timeline response.
    /// </summary>
    public class DayWeatherDto
    {
        public DateOnly Date { get; set; }
        public double TempMax { get; set; }
        public double TempMin { get; set; }
        public double Humidity { get; set; }
        public double PrecipProb { get; set; }
        public string Conditions { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
    }
}
