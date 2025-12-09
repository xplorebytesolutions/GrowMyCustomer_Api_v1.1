using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;

namespace xbytechat.api.Features.AutoReplyBuilder.Models
{
    public class AutoReplyFlowNode
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid FlowId { get; set; }

        [ForeignKey("FlowId")]
        public AutoReplyFlow Flow { get; set; }

        // 🔄 Use a constrained string or enum (recommended for future)
        [Required]
        public string NodeType { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string? NodeName { get; set; } // 🆕 Optional internal label for debugging

        [Required]
        public string ConfigJson { get; set; } = string.Empty;

        public Position Position { get; set; } = new();

        public int Order { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
