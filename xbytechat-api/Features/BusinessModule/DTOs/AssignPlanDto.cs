using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.BusinessModule.DTOs
{
    public class AssignPlanDto
    {
        [Required]
        public Guid PlanId { get; set; }
    }
}
