using System;

namespace xbytechat.api.Features.TeamStaff.DTOs
{
    /// <summary>
    /// Staff user list item for UI.
    /// </summary>
    public sealed class StaffUserDto
    {
        public Guid Id { get; set; }
        public Guid? BusinessId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public Guid? RoleId { get; set; }
        public string RoleName { get; set; } = "unknown";

        public string Status { get; set; } = "Pending"; // Active/Hold/Rejected/Pending
        public DateTime CreatedAt { get; set; }
    }
}
