// 📄 Features/AccessControl/DTOs/PermissionUpsertDto.cs
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.AccessControl.DTOs
{
    /// <summary>
    /// Payload for creating or updating a Permission.
    /// Code is immutable once created (UI disables it for edit).
    /// </summary>
    public sealed class PermissionUpsertDto
    {
        [Required]
        [MaxLength(200)]
        public string Code { get; set; } = default!; // e.g. "MESSAGING.SEND.TEXT"

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = default!; // friendly label

        [MaxLength(200)]
        public string? Group { get; set; } // "Messaging", "Campaigns", etc.

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
