using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CTAFlowBuilder.Models
{
    /// <summary>
    /// Logs when a visual flow step is executed (useful for analytics, debugging, audit).
    /// </summary>
    public class FlowExecutionLog
    {
        [Key]
        public Guid Id { get; set; }

        public Guid? RunId { get; set; }
        [Required]
        public Guid BusinessId { get; set; }

        [Required]
        public Guid StepId { get; set; }
        public string StepName { get; set; } = string.Empty;

        public Guid? FlowId { get; set; }

        public FlowExecutionOrigin Origin { get; set; } = FlowExecutionOrigin.Unknown; //new

        public Guid? CampaignId { get; set; } // new 
        public Guid? AutoReplyFlowId { get; set; } // new

        public Guid? CampaignSendLogId { get; set; }


        public Guid? TrackingLogId { get; set; }

        public string? ContactPhone { get; set; }

        public string? TriggeredByButton { get; set; }

        public string? TemplateName { get; set; }

        public string? TemplateType { get; set; }

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public string? RawResponse { get; set; }

        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        public Guid? MessageLogId { get; set; }              // tie to originating message
        public short? ButtonIndex { get; set; }              // which button was clicked (0..2)
        public Guid? RequestId { get; set; }

    }
}
