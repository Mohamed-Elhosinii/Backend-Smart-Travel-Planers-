using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Orchestrator.DTOs
{
    public class DayPlanDto
    {
        public int DayNumber { get; set; }
        public DateOnly Date { get; set; }
        public decimal BudgetAllocated { get; set; }
        public List<ActivityPlanDto> Activities { get; set; } = new();
    }
}
