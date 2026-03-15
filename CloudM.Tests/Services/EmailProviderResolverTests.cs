using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Resend;
using CloudM.Infrastructure.Services.Email;

namespace CloudM.Tests.Services
{
    public class EmailProviderResolverTests
    {
        [Fact]
        public void Resolve_DefaultProvider_UsesSmtpEmailService()
        {
            var provider = CreateServiceProvider(new Dictionary<string, string?>());

            var service = EmailProviderResolver.Resolve(provider);

            service.Should().BeOfType<EmailService>();
        }

        [Fact]
        public void Resolve_ResendProvider_UsesResendEmailService()
        {
            var provider = CreateServiceProvider(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Resend"
            });

            var service = EmailProviderResolver.Resolve(provider);

            service.Should().BeOfType<ResendEmailService>();
        }

        [Fact]
        public void Resolve_InvalidProvider_ThrowsInvalidOperationException()
        {
            var provider = CreateServiceProvider(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Unknown"
            });

            var act = () => EmailProviderResolver.Resolve(provider);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Email provider 'Unknown' is not supported. Use 'Smtp' or 'Resend'.");
        }

        private static ServiceProvider CreateServiceProvider(Dictionary<string, string?> configurationValues)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues)
                .Build();
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(new Mock<IResend>().Object);
            services.AddTransient<EmailService>();
            services.AddTransient<ResendEmailService>();

            return services.BuildServiceProvider();
        }
    }
}
