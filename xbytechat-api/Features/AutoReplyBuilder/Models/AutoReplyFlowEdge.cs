using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;

namespace xbytechat.api.Features.AutoReplyBuilder.Models
{
    public class AutoReplyFlowEdge
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid FlowId { get; set; }

        [ForeignKey("FlowId")]
        public AutoReplyFlow Flow { get; set; }

        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
    
        public string? SourceHandle { get; set; }
        public string? TargetHandle { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
