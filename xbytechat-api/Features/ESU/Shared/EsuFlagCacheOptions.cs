#nullable enable
namespace xbytechat.api.Features.ESU.Shared
{
    /// <summary>Cache knobs for ESU flag reads.</summary>
    public sealed class EsuFlagCacheOptions
    {
        /// <summary>Default TTL for positive cache hits (seconds). Keep short; flags change rarely but we want quick propagation.</summary>
        public int TtlSeconds { get; set; } = 30;

        /// <summary>TTL for negative lookups (misses). Prevents hammering DB when a flag is absent.</summary>
        public int MissTtlSeconds { get; set; } = 5;
    }
}
