using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.AccessControl.BusinessRoles.DTOs
{
    public sealed class BusinessRoleUpdateDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = "";

        [MaxLength(200)]
        public string? Description { get; set; }
    }
}
