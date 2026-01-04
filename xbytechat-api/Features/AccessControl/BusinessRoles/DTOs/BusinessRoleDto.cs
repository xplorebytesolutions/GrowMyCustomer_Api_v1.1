using System;

namespace xbytechat.api.Features.AccessControl.BusinessRoles.DTOs
{
    public sealed class BusinessRoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

