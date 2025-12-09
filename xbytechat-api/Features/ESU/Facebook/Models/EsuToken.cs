using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.ESU.Facebook.Models
{
    [Table("EsuTokens")]
    public sealed class EsuToken
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        [Required] public Guid BusinessId { get; set; }

        [Required, MaxLength(50)]
        public string Provider { get; set; } = "META_CLOUD"; // UPPERCASE canonical

        [Required, MaxLength(4096)]
        public string AccessToken { get; set; } = null!;

        public DateTime? ExpiresAtUtc { get; set; }
        [MaxLength(512)] public string? Scope { get; set; }
        public bool IsRevoked { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
