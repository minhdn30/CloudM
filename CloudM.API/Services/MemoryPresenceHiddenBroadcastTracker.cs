using Microsoft.Extensions.Caching.Memory;

namespace CloudM.API.Services
{
    public class MemoryPresenceHiddenBroadcastTracker : IDisposable
    {
        private const long MaxCacheEntries = 10000;
        private static readonly object[] KeyLockStripes = Enumerable
            .Range(0, 64)
            .Select(_ => new object())
            .ToArray();

        private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions
        {
            SizeLimit = MaxCacheEntries,
            CompactionPercentage = 0.2,
            ExpirationScanFrequency = TimeSpan.FromMinutes(1)
        });

        public bool TryAcquire(Guid accountId, TimeSpan ttl)
        {
            if (accountId == Guid.Empty)
            {
                return false;
            }

            var cacheKey = BuildCacheKey(accountId);
            lock (GetKeyLock(cacheKey))
            {
                if (_memoryCache.TryGetValue(cacheKey, out _))
                {
                    return false;
                }

                _memoryCache.Set(
                    cacheKey,
                    true,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(30),
                        Size = 1
                    });

                return true;
            }
        }

        public void Clear(Guid accountId)
        {
            if (accountId == Guid.Empty)
            {
                return;
            }

            _memoryCache.Remove(BuildCacheKey(accountId));
        }

        private static object GetKeyLock(string key)
        {
            var hash = (key?.GetHashCode() ?? 0) & int.MaxValue;
            return KeyLockStripes[hash % KeyLockStripes.Length];
        }

        private static string BuildCacheKey(Guid accountId)
        {
            return $"presence:hidden-fallback:{accountId:D}";
        }

        public void Dispose()
        {
            _memoryCache.Dispose();
        }
    }
}
