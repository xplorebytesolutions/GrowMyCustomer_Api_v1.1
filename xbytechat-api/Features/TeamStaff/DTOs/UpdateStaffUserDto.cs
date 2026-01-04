using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.TeamStaff.DTOs
{
    /// <summary>
    /// Update staff details (name + role). Status toggles are handled by dedicated endpoints.
    /// </summary>
    public sealed class UpdateStaffUserDto
    {
        [Required]
        [MinLength(2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public Guid RoleId { get; set; }
    }
}
