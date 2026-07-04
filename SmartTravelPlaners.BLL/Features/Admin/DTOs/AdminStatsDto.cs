using System;
using System.Collections.Generic;

namespace SmartTravelPlaners.BLL.Features.Admin.DTOs
{
    public class AdminStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalTrips { get; set; }
        public int ActiveSubscriptions { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<MonthlyRevenueDto> RevenueHistory { get; set; } = new();
        public List<DailyUserRegistrationDto> UserRegistrations { get; set; } = new();
    }

    public class MonthlyRevenueDto
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    public class DailyUserRegistrationDto
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
