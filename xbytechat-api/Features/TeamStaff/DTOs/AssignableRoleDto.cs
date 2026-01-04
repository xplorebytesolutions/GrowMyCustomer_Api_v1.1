using System;

namespace xbytechat.api.Features.TeamStaff.DTOs
{
    /// <summary>
    /// Minimal role payload for dropdowns (TeamStaff).
    /// </summary>
    public sealed class AssignableRoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "unknown";
    }
}
