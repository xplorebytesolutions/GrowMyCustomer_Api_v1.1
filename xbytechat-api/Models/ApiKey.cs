using System;

namespace xbytechat.api.Features.CustomeApi.Models
{
    public sealed class ApiKey
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }

        public string Name { get; set; } = string.Empty;   // e.g. "Zapier Prod Key"
        public string Prefix { get; set; } = string.Empty; // e.g. "live_ABCD"
        public string SecretHash { get; set; } = string.Empty;

        public string Scopes { get; set; } = "direct.send"; // comma- or space-separated scopes
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsRevoked { get; set; } = false;

        public string? CreatedBy { get; set; }
        public string? Notes { get; set; }
    }
}
