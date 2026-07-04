using Microsoft.Extensions.Logging;
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
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                _logger.LogInformation("Email send initiated. To: {ToEmail}, Subject: {Subject}", toEmail, subject);

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;
                email.Body = new TextPart("html") { Text = body };

                using var smtp = new SmtpClient();
                _logger.LogInformation("Connecting to SMTP server: {Host}:{Port}", _settings.Host, _settings.Port);
                await smtp.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.Password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to: {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to: {ToEmail}. Error: {ErrorMessage}", toEmail, ex.Message);
                throw;
            }
        }
    }
}