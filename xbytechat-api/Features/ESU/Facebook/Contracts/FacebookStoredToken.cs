#nullable enable
using System;

namespace xbytechat.api.Features.ESU.Facebook.Contracts
{
    public sealed class FacebookStoredToken
    {
        public string AccessToken { get; init; } = string.Empty;
        public DateTime? ExpiresAtUtc { get; init; }      // null = unknown
        public string? RawJson { get; init; }             // audit/debug snapshot

        /// <summary>Consider token invalid if it expires within this window (default 5 minutes).</summary>
        public bool WillExpireSoon(TimeSpan? skew = null)
        {
            if (ExpiresAtUtc is null) return false; // unknown -> assume fine, caller may decide stricter behavior
            var s = skew ?? TimeSpan.FromMinutes(5);
            return DateTime.UtcNow.Add(s) >= ExpiresAtUtc.Value;
        }

        public bool IsExpired()
            => ExpiresAtUtc is not null && DateTime.UtcNow >= ExpiresAtUtc.Value;
    }
}
