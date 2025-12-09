using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace xbytechat.api.Infrastructure.RateLimiting
{
    /// <summary>
    /// Token-bucket limiter per senderKey (Provider|PhoneNumberId).
    /// Default: 10 req/s with burst 10. You can tune dynamically.
    /// </summary>
    public interface IPhoneNumberRateLimiter
    {
        ValueTask<RateLimitLease> AcquireAsync(string senderKey, CancellationToken ct);
        void UpdateLimits(string senderKey, int permitsPerSecond, int burst);
    }

    public sealed class PhoneNumberRateLimiter : IPhoneNumberRateLimiter
    {
        private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _buckets = new();

        public ValueTask<RateLimitLease> AcquireAsync(string senderKey, CancellationToken ct)
        {
            var limiter = _buckets.GetOrAdd(senderKey, _ => Create(permitsPerSecond: 10, burst: 10));
            return limiter.AcquireAsync(1, ct);
        }

        public void UpdateLimits(string senderKey, int permitsPerSecond, int burst)
        {
            _buckets.AddOrUpdate(senderKey,
                _ => Create(permitsPerSecond, burst),
                (_, __) => Create(permitsPerSecond, burst));
        }

        private static TokenBucketRateLimiter Create(int permitsPerSecond, int burst)
        {
            return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = Math.Max(1, burst),
                TokensPerPeriod = Math.Max(1, permitsPerSecond),
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),   // ← correct property name
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0                                    // no internal queue; your outbox already queues
            });
        }
    }
}
