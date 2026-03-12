using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using CloudM.Application.Services.PresenceServices;

namespace CloudM.API.Services
{
    public class MemoryPresenceSnapshotRateLimiter : IDisposable
    {
        private const long MaxCacheEntries = 20000;
        private static readonly object[] KeyLockStripes = Enumerable
            .Range(0, 64)
            .Select(_ => new object())
            .ToArray();

        private readonly MemoryCache _memoryCache;
        private readonly OnlinePresenceOptions _options;

        public MemoryPresenceSnapshotRateLimiter(
            IOptions<OnlinePresenceOptions> options)
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = MaxCacheEntries,
                CompactionPercentage = 0.2,
                ExpirationScanFrequency = TimeSpan.FromMinutes(1)
            });
            _options = (options.Value ?? new OnlinePresenceOptions()).Normalize();
        }

        public (bool Allowed, int RetryAfterSeconds) TryConsume(Guid viewerAccountId, DateTime nowUtc)
        {
            if (viewerAccountId == Guid.Empty)
            {
                return (false, 1);
            }

            var normalizedNowUtc = NormalizeUtc(nowUtc);
            var windowSeconds = Math.Max(10, _options.SnapshotRateLimitWindowSeconds);
            var maxRequests = Math.Max(1, _options.SnapshotRateLimitMaxRequests);
            var nowUnix = new DateTimeOffset(normalizedNowUtc).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;
            var retryAfterSeconds = Math.Max(1, (int)((bucket + 1) * windowSeconds - nowUnix));
            var cacheKey = BuildCacheKey(viewerAccountId, bucket);

            lock (GetKeyLock(cacheKey))
            {
                long requestCount = 0;
                if (_memoryCache.TryGetValue<long>(cacheKey, out var cachedCount))
                {
                    requestCount = cachedCount;
                }

                requestCount += 1;
                _memoryCache.Set(
                    cacheKey,
                    requestCount,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(retryAfterSeconds + 5),
                        Size = 1
                    });

                if (requestCount <= maxRequests)
                {
                    return (true, 0);
                }

                return (false, retryAfterSeconds);
            }
        }

        private static object GetKeyLock(string key)
        {
            var hash = (key?.GetHashCode() ?? 0) & int.MaxValue;
            return KeyLockStripes[hash % KeyLockStripes.Length];
        }

        private static string BuildCacheKey(Guid viewerAccountId, long bucket)
        {
            return $"presence:snapshot:memory-rl:{viewerAccountId:D}:w:{bucket}";
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public void Dispose()
        {
            _memoryCache.Dispose();
        }
    }
}
