using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.AccessControl.DTOs.UserPermissions
{
    public sealed class UserPermissionSummaryDto
    {
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = default!;
        public Guid BusinessId { get; set; }
        public Guid PlanId { get; set; }

        public List<UserPermissionItemDto> Items { get; set; } = new();
    }
}
