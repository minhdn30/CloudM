using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CloudM.Infrastructure.Services.Email
{
    public static class EmailProviderResolver
    {
        public static IEmailService Resolve(IServiceProvider provider)
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var emailProvider = configuration["Email:Provider"];

            if (string.IsNullOrWhiteSpace(emailProvider)
                || string.Equals(emailProvider, "smtp", StringComparison.OrdinalIgnoreCase))
            {
                return provider.GetRequiredService<EmailService>();
            }

            if (string.Equals(emailProvider, "resend", StringComparison.OrdinalIgnoreCase))
            {
                return provider.GetRequiredService<ResendEmailService>();
            }

            throw new InvalidOperationException(
                $"Email provider '{emailProvider}' is not supported. Use 'Smtp' or 'Resend'.");
        }
    }
}
