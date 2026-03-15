using CloudM.Application.DTOs.PresenceDTOs;
using CloudM.Application.Services.PresenceServices;
using CloudM.Domain.Enums;

namespace CloudM.API.Services
{
    public class NoOpOnlinePresenceService : IOnlinePresenceService
    {
        private readonly MemoryPresenceSnapshotRateLimiter _memorySnapshotRateLimiter;

        public NoOpOnlinePresenceService(MemoryPresenceSnapshotRateLimiter memorySnapshotRateLimiter)
        {
            _memorySnapshotRateLimiter = memorySnapshotRateLimiter;
        }

        public Task MarkConnectedAsync(Guid accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkDisconnectedAsync(Guid? accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TouchHeartbeatAsync(Guid accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<(bool Allowed, int RetryAfterSeconds)> TryConsumeSnapshotRateLimitAsync(
            Guid viewerAccountId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_memorySnapshotRateLimiter.TryConsume(viewerAccountId, nowUtc));
        }

        public Task<PresenceSnapshotResponse> GetSnapshotAsync(
            Guid viewerAccountId,
            IReadOnlyCollection<Guid> accountIds,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var normalizedAccountIds = (accountIds ?? Array.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var items = normalizedAccountIds
                .Select(accountId => new PresenceSnapshotItemResponse
                {
                    AccountId = accountId,
                    CanShowStatus = false,
                    IsOnline = false,
                    LastOnlineAt = null
                })
                .ToList();

            return Task.FromResult(new PresenceSnapshotResponse
            {
                Items = items
            });
        }

        public Task NotifyBlockedPairHiddenAsync(
            Guid currentId,
            Guid targetId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyVisibilityChangedAsync(
            Guid accountId,
            OnlineStatusVisibilityEnum previousVisibility,
            OnlineStatusVisibilityEnum currentVisibility,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> ProcessOfflineCandidatesAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}
