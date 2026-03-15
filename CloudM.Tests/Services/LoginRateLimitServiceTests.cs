using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using CloudM.Application.Services.AuthServices;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class LoginRateLimitServiceTests
    {
        [Fact]
        public async Task MemoryLoginRateLimitService_ExceededAttempts_LocksAccount()
        {
            var service = new MemoryLoginRateLimitService(
                CreateOptions(maxFailedAttemptsPerEmailWindow: 1),
                new ConfigurationBuilder().AddInMemoryCollection().Build());

            var nowUtc = DateTime.UtcNow;

            await service.RecordFailedAttemptAsync("user@test.com", null, nowUtc);

            var act = () => service.EnforceLoginAllowedAsync("user@test.com", null, nowUtc);

            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Too many failed login attempts for this account.*");
        }

        [Fact]
        public async Task RedisLoginRateLimitService_RedisFails_FallsBackToMemoryLock()
        {
            var redisException = new RedisConnectionException(
                ConnectionFailureType.SocketFailure,
                "redis down");
            var redisMock = new Mock<IConnectionMultiplexer>();
            var databaseMock = new Mock<IDatabase>();
            var memoryFallbackService = new MemoryLoginRateLimitService(
                CreateOptions(maxFailedAttemptsPerEmailWindow: 1),
                new ConfigurationBuilder().AddInMemoryCollection().Build());

            redisMock
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(databaseMock.Object);
            databaseMock
                .Setup(x => x.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(redisException);
            databaseMock
                .Setup(x => x.KeyTimeToLiveAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(redisException);

            var service = new RedisLoginRateLimitService(
                redisMock.Object,
                memoryFallbackService,
                CreateOptions(maxFailedAttemptsPerEmailWindow: 1),
                new ConfigurationBuilder().AddInMemoryCollection().Build());

            var nowUtc = DateTime.UtcNow;

            await service.RecordFailedAttemptAsync("user@test.com", null, nowUtc);

            var act = () => service.EnforceLoginAllowedAsync("user@test.com", null, nowUtc);

            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage("Too many failed login attempts for this account.*");
        }

        private static IOptions<LoginSecurityOptions> CreateOptions(int maxFailedAttemptsPerEmailWindow)
        {
            return Options.Create(new LoginSecurityOptions
            {
                MaxFailedAttemptsPerEmailWindow = maxFailedAttemptsPerEmailWindow,
                EmailWindowMinutes = 15,
                MaxFailedAttemptsPerIpWindow = 50,
                IpWindowMinutes = 15,
                MaxFailedAttemptsPerEmailIpWindow = 5,
                EmailIpWindowMinutes = 15,
                LockMinutes = 15
            });
        }
    }
}
