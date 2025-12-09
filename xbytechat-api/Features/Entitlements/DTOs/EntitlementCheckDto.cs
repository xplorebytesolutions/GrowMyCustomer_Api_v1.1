using System.Collections.Generic;

namespace xbytechat.api.Features.Entitlements.DTOs
{
    public sealed class EntitlementCheckDto
    {
        public string QuotaKey { get; set; } = default!;
        public long Amount { get; set; } = 1;
        public bool ConsumeOnSuccess { get; set; } = true;
    }

    public sealed class EntitlementResultDto
    {
        public bool Allowed { get; set; }
        public string QuotaKey { get; set; } = default!;
        public long? Limit { get; set; }           // null if unlimited
        public long? Remaining { get; set; }       // null if unlimited
        public string? Message { get; set; }
    }

    public sealed class EntitlementsSnapshotDto
    {
        public IEnumerable<string> GrantedPermissions { get; set; } = new List<string>();
        public IEnumerable<QuotaSnapshotItemDto> Quotas { get; set; } = new List<QuotaSnapshotItemDto>();
    }

    public sealed class QuotaSnapshotItemDto
    {
        public string QuotaKey { get; set; } = default!;
        public string Period { get; set; } = default!; // "Daily"/"Monthly"/"Lifetime"
        public long? Limit { get; set; }               // null => unlimited
        public long Consumed { get; set; }
        public long? Remaining { get; set; }           // null => unlimited
        public string? DenialMessage { get; set; }
        public string WindowStartUtc { get; set; } = default!;
    }
}
