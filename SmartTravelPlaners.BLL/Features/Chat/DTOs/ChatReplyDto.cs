using SmartTravelPlaners.BLL.Features.Orchestrator.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Features.Chat.DTOs
{
    public class ChatReplyDto
    {
        public string Message { get; set; } = string.Empty;
        public TripPlanDto? Plan { get; set; }
    }
}
