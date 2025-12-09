using System;

namespace xbytechat.api.Features.AccessControl.DTOs
{
    public class CreatePlanDto
    {
        public string Code { get; set; } // e.g. "FREE", "SMART"
        public string Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
