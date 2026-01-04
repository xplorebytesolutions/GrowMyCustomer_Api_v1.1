using System;

namespace xbytechat.api.Features.Entitlements.DTOs
{
    public sealed class UpsertBusinessPermissionOverrideDto
    {
        public string PermissionCode { get; set; } = "";
        public bool IsGranted { get; set; }           // true = grant, false = deny
        public string? Reason { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }   // optional temporary unlock
    }
}
