using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace NexusOXP.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options?.Value ?? new EmailOptions();
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentException("Recipient address (to) must be provided.", nameof(to));
            }

            try
            {
                if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromAddress))
                {
                    _logger.LogWarning("SMTP email configuration is missing. Email was not sent. Set Smtp:Host and Smtp:FromAddress.");
                    _logger.LogInformation("Email dry-run: To={To}; Subject={Subject}; Body={Body}", to, subject, body);
                    return;
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(_options.FromAddress, _options.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml,
                };

                message.To.Add(to);

                using var client = new SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = _options.EnableSsl,
                    UseDefaultCredentials = false,
                    Credentials = string.IsNullOrWhiteSpace(_options.Username)
                        ? null
                        : new NetworkCredential(_options.Username, _options.Password)
                };

                _logger.LogInformation("Sending email to {To} via {Host}:{Port}", to, _options.Host, _options.Port);
                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {To}", to);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP error sending email to {To}: {Message}", to, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email to {To}", to);
                throw;
            }
        }
    }
}
