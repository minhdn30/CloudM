using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Resend;
using CloudM.Infrastructure.Services.Email;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class ResendEmailServiceTests
    {
        [Fact]
        public async Task SendEmailAsync_MissingResendApiKey_ThrowsInternalServerException()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:FromEmail"] = "noreply@test.com"
                })
                .Build();
            var resendMock = new Mock<IResend>();
            var service = new ResendEmailService(configuration, resendMock.Object);

            var act = () => service.SendEmailAsync("user@test.com", "subject", "<strong>body</strong>", true);

            await act.Should().ThrowAsync<InternalServerException>()
                .WithMessage("Email configuration is missing. Please check ResendApiKey and FromEmail settings.");
        }

        [Fact]
        public async Task SendEmailAsync_MissingFromEmail_ThrowsInternalServerException()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:ResendApiKey"] = "re_test_key"
                })
                .Build();
            var resendMock = new Mock<IResend>();
            var service = new ResendEmailService(configuration, resendMock.Object);

            var act = () => service.SendEmailAsync("user@test.com", "subject", "body");

            await act.Should().ThrowAsync<InternalServerException>()
                .WithMessage("Email configuration is missing. Please check ResendApiKey and FromEmail settings.");
        }
    }
}
