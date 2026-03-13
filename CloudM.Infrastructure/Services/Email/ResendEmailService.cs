using Microsoft.Extensions.Configuration;
using Resend;
using System;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Infrastructure.Services.Email
{
    public class ResendEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly IResend _resend;

        public ResendEmailService(IConfiguration config, IResend resend)
        {
            _config = config;
            _resend = resend;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            var resendApiKey = _config["Email:ResendApiKey"];
            var fromEmail = _config["Email:FromEmail"];

            if (string.IsNullOrWhiteSpace(resendApiKey) || string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InternalServerException("Email configuration is missing. Please check ResendApiKey and FromEmail settings.");
            }

            var message = new EmailMessage
            {
                From = fromEmail,
                Subject = subject
            };
            message.To.Add(to);

            if (isHtml)
            {
                message.HtmlBody = body;
            }
            else
            {
                message.TextBody = body;
            }

            try
            {
                await _resend.EmailSendAsync(message);
            }
            catch (Exception ex)
            {
                throw new InternalServerException($"Failed to send email: {ex.Message}");
            }
        }
    }
}
