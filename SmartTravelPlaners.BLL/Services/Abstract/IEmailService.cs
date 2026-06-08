using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Services.Abstract
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}
