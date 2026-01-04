using System;

namespace xbytechat.api.Features.Entitlements.DTOs
{
    public sealed class BusinessPermissionOverrideDto
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public string PermissionCode { get; set; } = "";
        public bool IsGranted { get; set; }
        public bool IsRevoked { get; set; }
        public string? Reason { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
