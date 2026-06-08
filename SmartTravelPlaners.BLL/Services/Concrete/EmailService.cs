using Microsoft.Extensions.Options;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.Services.Abstract;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks; 
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SmartTravelPlaners.BLL.Services.Concrete
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart("html") { Text = body };

            using var smtp = new SmtpClient(); 
            await smtp.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}