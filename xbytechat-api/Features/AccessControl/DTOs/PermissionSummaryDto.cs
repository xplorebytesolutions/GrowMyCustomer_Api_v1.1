// 📄 Features/AccessControl/DTOs/PermissionSummaryDto.cs
using System;

namespace xbytechat.api.Features.AccessControl.DTOs
{
    /// <summary>
    /// Flat DTO used by the Permissions admin grid.
    /// </summary>
    public sealed class PermissionSummaryDto
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;

        public string? Group { get; set; }
        public string? Description { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
