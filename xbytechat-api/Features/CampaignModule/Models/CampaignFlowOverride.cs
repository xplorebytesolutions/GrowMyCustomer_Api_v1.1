using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CampaignModule.Models
{
    public class CampaignFlowOverride
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CampaignId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ButtonText { get; set; } = string.Empty;

        public string? OverrideNextTemplate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }

        [ForeignKey("CampaignId")]
        public Campaign? Campaign { get; set; }
    }
}
