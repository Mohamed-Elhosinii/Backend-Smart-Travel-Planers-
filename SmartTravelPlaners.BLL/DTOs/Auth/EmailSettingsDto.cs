using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.DTOs.Auth
{
    public class EmailSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string SenderEmail { get; set; }
        public string SenderName { get; set; }
        public string Password { get; set; }
    }
}
