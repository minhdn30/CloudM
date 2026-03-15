using FluentAssertions;
using Microsoft.Extensions.Options;
using CloudM.API.Services;
using CloudM.Application.Services.EmailVerificationServices;
using CloudM.Application.Services.PresenceServices;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Tests.Services
{
    public class RedisFallbackServicesTests
    {
        [Fact]
        public async Task UnavailableEmailVerificationRateLimitService_EnforceSendRateLimitAsync_ThrowsInternalServerException()
        {
            var service = new UnavailableEmailVerificationRateLimitService();

            await FluentActions.Invoking(() => service.EnforceSendRateLimitAsync("user@test.com", "127.0.0.1", DateTime.UtcNow))
                .Should()
                .ThrowAsync<InternalServerException>();
        }

        [Fact]
        public async Task NoOpOnlinePresenceService_GetSnapshotAsync_ReturnsHiddenOfflineItems()
        {
            var service = new NoOpOnlinePresenceService(CreateSnapshotRateLimiter());
            var firstAccountId = Guid.NewGuid();
            var secondAccountId = Guid.NewGuid();

            var result = await service.GetSnapshotAsync(
                Guid.NewGuid(),
                new List<Guid> { firstAccountId, Guid.Empty, firstAccountId, secondAccountId },
                DateTime.UtcNow);

            result.Items.Should().HaveCount(2);
            result.Items.Should().OnlyContain(item =>
                item.AccountId != Guid.Empty
                && item.CanShowStatus == false
                && item.IsOnline == false
                && item.LastOnlineAt == null);
        }

        [Fact]
        public async Task NoOpOnlinePresenceService_Operations_AreSafeNoOps()
        {
            var service = new NoOpOnlinePresenceService(CreateSnapshotRateLimiter());
            var accountId = Guid.NewGuid();
            var nowUtc = DateTime.UtcNow;

            await FluentActions.Invoking(() => service.MarkConnectedAsync(accountId, "connection-1", nowUtc))
                .Should()
                .NotThrowAsync();

            await FluentActions.Invoking(() => service.MarkDisconnectedAsync(accountId, "connection-1", nowUtc))
                .Should()
                .NotThrowAsync();

            await FluentActions.Invoking(() => service.TouchHeartbeatAsync(accountId, "connection-1", nowUtc))
                .Should()
                .NotThrowAsync();

            await FluentActions.Invoking(() => service.NotifyBlockedPairHiddenAsync(accountId, Guid.NewGuid()))
                .Should()
                .NotThrowAsync();

            await FluentActions.Invoking(() => service.NotifyVisibilityChangedAsync(
                    accountId,
                    Domain.Enums.OnlineStatusVisibilityEnum.ContactsOnly,
                    Domain.Enums.OnlineStatusVisibilityEnum.NoOne,
                    nowUtc))
                .Should()
                .NotThrowAsync();

            var rateLimitResult = await service.TryConsumeSnapshotRateLimitAsync(accountId, nowUtc);
            rateLimitResult.Allowed.Should().BeTrue();
            rateLimitResult.RetryAfterSeconds.Should().Be(0);

            var processed = await service.ProcessOfflineCandidatesAsync(nowUtc, 100);
            processed.Should().Be(0);
        }

        [Fact]
        public async Task NoOpOnlinePresenceService_SnapshotRateLimit_UsesMemoryFallbackWindow()
        {
            var service = new NoOpOnlinePresenceService(CreateSnapshotRateLimiter(maxRequests: 1));
            var accountId = Guid.NewGuid();
            var nowUtc = DateTime.UtcNow;

            var firstResult = await service.TryConsumeSnapshotRateLimitAsync(accountId, nowUtc);
            var secondResult = await service.TryConsumeSnapshotRateLimitAsync(accountId, nowUtc);

            firstResult.Allowed.Should().BeTrue();
            firstResult.RetryAfterSeconds.Should().Be(0);
            secondResult.Allowed.Should().BeFalse();
            secondResult.RetryAfterSeconds.Should().BeGreaterThan(0);
        }

        private static MemoryPresenceSnapshotRateLimiter CreateSnapshotRateLimiter(int maxRequests = 60)
        {
            return new MemoryPresenceSnapshotRateLimiter(
                Options.Create(new OnlinePresenceOptions
                {
                    SnapshotRateLimitWindowSeconds = 30,
                    SnapshotRateLimitMaxRequests = maxRequests
                }));
        }
    }
}
