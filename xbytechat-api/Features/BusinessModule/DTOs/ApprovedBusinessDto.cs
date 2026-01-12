using System;

namespace xbytechat.api.Features.BusinessModule.DTOs
{
    public class ApprovedBusinessDto
    {
        public Guid Id { get; set; }
        public string CompanyName { get; set; }
        public string BusinessEmail { get; set; }
        public Guid? PlanId { get; set; }
        public string? PlanName { get; set; }
        public string? LogoUrl { get; set; }
    }
}
