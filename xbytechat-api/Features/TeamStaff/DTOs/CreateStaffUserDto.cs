using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.TeamStaff.DTOs
{
    /// <summary>
    /// Create a staff user under the current Business.
    /// </summary>
    public sealed class CreateStaffUserDto
    {
        [Required]
        [MinLength(2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Raw password (MVP). We will hash it using the same SHA256 strategy as AuthService.
        /// Future enhancement: switch to BCrypt/Identity.
        /// </summary>
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// RoleId for the staff user (e.g., agent/staff/manager role).
        /// </summary>
        [Required]
        public Guid RoleId { get; set; }
    }
}
