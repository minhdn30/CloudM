using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using CloudM.Application.Services.AuthServices;
using CloudM.Application.Services.EmailVerificationServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.EmailVerifications;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Infrastructure.Services.Email;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class PasswordResetServiceTests
    {
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IAccountRepository> _accountRepositoryMock;
        private readonly Mock<IEmailVerificationRepository> _emailVerificationRepositoryMock;
        private readonly Mock<IEmailVerificationRateLimitService> _rateLimitServiceMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly PasswordResetService _service;

        public PasswordResetServiceTests()
        {
            _emailServiceMock = new Mock<IEmailService>();
            _accountRepositoryMock = new Mock<IAccountRepository>();
            _emailVerificationRepositoryMock = new Mock<IEmailVerificationRepository>();
            _rateLimitServiceMock = new Mock<IEmailVerificationRateLimitService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            var options = Options.Create(new EmailVerificationSecurityOptions
            {
                OtpExpiresMinutes = 5,
                ResendCooldownSeconds = 60,
                MaxSendsPerWindow = 3,
                SendWindowMinutes = 15,
                MaxSendsPerDay = 10,
                MaxFailedAttempts = 5,
                LockMinutes = 15,
                OtpPepper = "TEST_OTP_PEPPER"
            });

            _unitOfWorkMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            _service = new PasswordResetService(
                _emailServiceMock.Object,
                _accountRepositoryMock.Object,
                _emailVerificationRepositoryMock.Object,
                _rateLimitServiceMock.Object,
                _unitOfWorkMock.Object,
                options);
        }

        [Fact]
        public async Task SendResetPasswordCodeAsync_RateLimitUnavailable_FallsBackToSqlRateLimitAndSendsEmail()
        {
            var email = "reset-fallback@test.com";
            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                Email = email,
                Status = AccountStatusEnum.Active
            };
            var verification = new EmailVerification
            {
                Id = 30,
                Email = email,
                LastSentAt = DateTime.UtcNow.AddMinutes(-10),
                SendWindowStartedAt = DateTime.UtcNow.AddMinutes(-20),
                SendCountInWindow = 0,
                DailyWindowStartedAt = DateTime.UtcNow.Date,
                DailySendCount = 0,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                CodeHash = string.Empty,
                CodeSalt = string.Empty
            };

            _accountRepositoryMock
                .Setup(x => x.GetAccountByEmail(email))
                .ReturnsAsync(account);
            _rateLimitServiceMock
                .Setup(x => x.EnforceSendRateLimitAsync(email, It.IsAny<string?>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InternalServerException("Email verification rate limit is unavailable."));
            _emailVerificationRepositoryMock
                .Setup(x => x.EnsureExistsByEmailAsync(email, It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask);
            _emailVerificationRepositoryMock
                .Setup(x => x.GetLatestByEmailAsync(email))
                .ReturnsAsync(verification);
            _emailServiceMock
                .Setup(x => x.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>(), true))
                .Returns(Task.CompletedTask);

            await _service.SendResetPasswordCodeAsync(email, "127.0.0.1");

            verification.SendCountInWindow.Should().Be(1);
            verification.DailySendCount.Should().Be(1);
            _emailServiceMock.Verify(
                x => x.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>(), true),
                Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }
    }
}
