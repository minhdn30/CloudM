using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AuthServices
{
    public class MemoryLoginRateLimitService : ILoginRateLimitService, IDisposable
    {
        private const long MaxCacheEntries = 50000;
        private static readonly object[] KeyLockStripes = Enumerable
            .Range(0, 64)
            .Select(_ => new object())
            .ToArray();

        private readonly MemoryCache _memoryCache;
        private readonly LoginSecurityOptions _options;
        private readonly string _keyPrefix;

        public MemoryLoginRateLimitService(
            IOptions<LoginSecurityOptions> options,
            IConfiguration configuration)
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = MaxCacheEntries,
                CompactionPercentage = 0.2,
                ExpirationScanFrequency = TimeSpan.FromMinutes(1)
            });
            _options = NormalizeOptions(options.Value);
            _keyPrefix = (configuration["Redis:KeyPrefix"] ?? "cloudm").Trim();
        }

        public Task EnforceLoginAllowedAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIp = NormalizeIp(ipAddress);

            EnsureNotLocked(
                BuildEmailLockKey(normalizedEmail),
                _options.LockMinutes,
                "Too many failed login attempts for this account.",
                nowUtc);

            if (string.IsNullOrWhiteSpace(normalizedIp))
            {
                return Task.CompletedTask;
            }

            EnsureNotLocked(
                BuildIpLockKey(normalizedIp),
                _options.LockMinutes,
                "Too many failed login attempts from this network.",
                nowUtc);

            if (_options.MaxFailedAttemptsPerEmailIpWindow > 0)
            {
                EnsureNotLocked(
                    BuildEmailIpLockKey(normalizedEmail, normalizedIp),
                    _options.LockMinutes,
                    "Too many failed login attempts for this account from this network.",
                    nowUtc);
            }

            return Task.CompletedTask;
        }

        public Task RecordFailedAttemptAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIp = NormalizeIp(ipAddress);

            var emailFailedCount = IncrementFixedWindowCounter(
                BuildEmailFailureCounterPrefix(normalizedEmail),
                _options.EmailWindowMinutes,
                nowUtc);

            if (emailFailedCount >= _options.MaxFailedAttemptsPerEmailWindow)
            {
                SetLock(BuildEmailLockKey(normalizedEmail), _options.LockMinutes, nowUtc);
            }

            if (string.IsNullOrWhiteSpace(normalizedIp))
            {
                return Task.CompletedTask;
            }

            var ipFailedCount = IncrementFixedWindowCounter(
                BuildIpFailureCounterPrefix(normalizedIp),
                _options.IpWindowMinutes,
                nowUtc);

            if (ipFailedCount >= _options.MaxFailedAttemptsPerIpWindow)
            {
                SetLock(BuildIpLockKey(normalizedIp), _options.LockMinutes, nowUtc);
            }

            if (_options.MaxFailedAttemptsPerEmailIpWindow > 0)
            {
                var emailIpFailedCount = IncrementFixedWindowCounter(
                    BuildEmailIpFailureCounterPrefix(normalizedEmail, normalizedIp),
                    _options.EmailIpWindowMinutes,
                    nowUtc);

                if (emailIpFailedCount >= _options.MaxFailedAttemptsPerEmailIpWindow)
                {
                    SetLock(BuildEmailIpLockKey(normalizedEmail, normalizedIp), _options.LockMinutes, nowUtc);
                }
            }

            return Task.CompletedTask;
        }

        public Task ClearFailedAttemptsAsync(string email, string? ipAddress, DateTime nowUtc)
        {
            var normalizedEmail = NormalizeEmail(email);
            var normalizedIp = NormalizeIp(ipAddress);

            _memoryCache.Remove(BuildEmailLockKey(normalizedEmail));
            _memoryCache.Remove(
                BuildCurrentWindowKey(
                    BuildEmailFailureCounterPrefix(normalizedEmail),
                    _options.EmailWindowMinutes,
                    nowUtc));

            if (!string.IsNullOrWhiteSpace(normalizedIp) && _options.MaxFailedAttemptsPerEmailIpWindow > 0)
            {
                _memoryCache.Remove(BuildEmailIpLockKey(normalizedEmail, normalizedIp));
                _memoryCache.Remove(
                    BuildCurrentWindowKey(
                        BuildEmailIpFailureCounterPrefix(normalizedEmail, normalizedIp),
                        _options.EmailIpWindowMinutes,
                        nowUtc));
            }

            return Task.CompletedTask;
        }

        private void EnsureNotLocked(string lockKey, int lockMinutes, string lockMessage, DateTime nowUtc)
        {
            if (!_memoryCache.TryGetValue<DateTime>(lockKey, out var expiresAtUtc))
            {
                return;
            }

            var normalizedNowUtc = NormalizeUtc(nowUtc);
            if (expiresAtUtc > normalizedNowUtc)
            {
                var remainingSeconds = Math.Max(1, (int)Math.Ceiling((expiresAtUtc - normalizedNowUtc).TotalSeconds));
                throw new UnauthorizedException($"{lockMessage} Please wait {remainingSeconds} seconds and try again.");
            }

            _memoryCache.Remove(lockKey);
        }

        private long IncrementFixedWindowCounter(string keyPrefix, int windowMinutes, DateTime nowUtc)
        {
            var key = BuildCurrentWindowKey(keyPrefix, windowMinutes, nowUtc);
            var keyLock = GetKeyLock(key);
            lock (keyLock)
            {
                long currentCount = 0;
                if (_memoryCache.TryGetValue<long>(key, out var cachedCount))
                {
                    currentCount = cachedCount;
                }

                currentCount += 1;

                var windowSeconds = Math.Max(60, windowMinutes * 60);
                var nowUnix = new DateTimeOffset(NormalizeUtc(nowUtc)).ToUnixTimeSeconds();
                var bucket = nowUnix / windowSeconds;
                var ttlSeconds = Math.Max(1, (bucket + 1) * windowSeconds - nowUnix + 30);

                SetEntry(key, currentCount, TimeSpan.FromSeconds(ttlSeconds));
                return currentCount;
            }
        }

        private static object GetKeyLock(string key)
        {
            var hash = (key?.GetHashCode() ?? 0) & int.MaxValue;
            return KeyLockStripes[hash % KeyLockStripes.Length];
        }

        private void SetLock(string key, int lockMinutes, DateTime nowUtc)
        {
            var expires = TimeSpan.FromMinutes(Math.Max(1, lockMinutes));
            SetEntry(key, NormalizeUtc(nowUtc).Add(expires), expires);
        }

        private void SetEntry<TValue>(string key, TValue value, TimeSpan ttl)
        {
            _memoryCache.Set(
                key,
                value,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    Size = 1
                });
        }

        private static string BuildCurrentWindowKey(string keyPrefix, int windowMinutes, DateTime nowUtc)
        {
            var windowSeconds = Math.Max(60, windowMinutes * 60);
            var nowUnix = new DateTimeOffset(NormalizeUtc(nowUtc)).ToUnixTimeSeconds();
            var bucket = nowUnix / windowSeconds;

            return $"{keyPrefix}:w:{bucket}";
        }

        private string BuildEmailLockKey(string email)
        {
            return $"{_keyPrefix}:auth:login:lock:email:{email}";
        }

        private string BuildIpLockKey(string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:lock:ip:{ipAddress}";
        }

        private string BuildEmailIpLockKey(string email, string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:lock:email-ip:{email}:{ipAddress}";
        }

        private string BuildEmailFailureCounterPrefix(string email)
        {
            return $"{_keyPrefix}:auth:login:fail:email:{email}";
        }

        private string BuildIpFailureCounterPrefix(string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:fail:ip:{ipAddress}";
        }

        private string BuildEmailIpFailureCounterPrefix(string email, string ipAddress)
        {
            return $"{_keyPrefix}:auth:login:fail:email-ip:{email}:{ipAddress}";
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string? NormalizeIp(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            var candidate = ipAddress.Split(',')[0].Trim();
            if (!IPAddress.TryParse(candidate, out var parsedIp))
            {
                return null;
            }

            if (parsedIp.IsIPv4MappedToIPv6)
            {
                parsedIp = parsedIp.MapToIPv4();
            }

            return parsedIp.ToString();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static LoginSecurityOptions NormalizeOptions(LoginSecurityOptions options)
        {
            options.MaxFailedAttemptsPerEmailWindow = options.MaxFailedAttemptsPerEmailWindow <= 0
                ? 10
                : options.MaxFailedAttemptsPerEmailWindow;
            options.EmailWindowMinutes = options.EmailWindowMinutes <= 0 ? 15 : options.EmailWindowMinutes;
            options.MaxFailedAttemptsPerIpWindow = options.MaxFailedAttemptsPerIpWindow <= 0
                ? 50
                : options.MaxFailedAttemptsPerIpWindow;
            options.IpWindowMinutes = options.IpWindowMinutes <= 0 ? 15 : options.IpWindowMinutes;
            options.MaxFailedAttemptsPerEmailIpWindow = options.MaxFailedAttemptsPerEmailIpWindow < 0
                ? 5
                : options.MaxFailedAttemptsPerEmailIpWindow;
            options.EmailIpWindowMinutes = options.EmailIpWindowMinutes <= 0 ? 15 : options.EmailIpWindowMinutes;
            options.LockMinutes = options.LockMinutes <= 0 ? 15 : options.LockMinutes;

            return options;
        }

        public void Dispose()
        {
            _memoryCache.Dispose();
        }
    }
}
