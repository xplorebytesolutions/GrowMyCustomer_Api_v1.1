using System;
using System.Collections.Generic;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CTAManagement.Models;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.CTAFlowBuilder.Models; // 🆕 Import for CTAFlowConfig

namespace xbytechat.api.Features.CampaignModule.Models
{
    public class Campaign
    {
        public Guid Id { get; set; }

        // 🔗 Business info
        public Guid BusinessId { get; set; }
        public Business Business { get; set; }
        public Guid? CampaignId { get; set; }
        public Campaign? SourceCampaign { get; set; }

        // 📋 Core campaign details
        public string Name { get; set; }
        public string MessageTemplate { get; set; }
        public string? TemplateId { get; set; } // ✅ Meta-approved template ID

        [Column(TypeName = "text")]
        public string? MessageBody { get; set; } // ✅ Final resolved WhatsApp message body

        public string? FollowUpTemplateId { get; set; }
        public string? CampaignType { get; set; } // text, template, cta

        // 🔘 CTA tracking (optional)
        public Guid? CtaId { get; set; }
        public CTADefinition? Cta { get; set; }

        // 🆕 Link to Flow Config (optional)
        public Guid? CTAFlowConfigId { get; set; }
       // [ForeignKey(nameof(CTAFlowConfigId))]
        public CTAFlowConfig? CTAFlowConfig { get; set; }

        public DateTime? ScheduledAt { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Scheduled, Sent

        // 👤 Metadata
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🗑️ Soft delete support
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        // 👥 Recipient relationship
        public ICollection<CampaignRecipient> Recipients { get; set; }

        // 📊 Logs
        public ICollection<CampaignSendLog> SendLogs { get; set; } = new List<CampaignSendLog>();
        public ICollection<MessageStatusLog> MessageStatusLogs { get; set; }

        public string? ImageUrl { get; set; }
        public string? ImageCaption { get; set; }
        public string? TemplateParameters { get; set; }

        public ICollection<CampaignButton> MultiButtons { get; set; } = new List<CampaignButton>();

        public ICollection<MessageLog> MessageLogs { get; set; } = new List<MessageLog>();

        public string? Provider { get; set; }            // UPPERCASE only
        public string? PhoneNumberId { get; set; }

        public string? TemplateSchemaSnapshot { get; set; }

        public ICollection<CampaignVariableMap> VariableMaps { get; set; } = new List<CampaignVariableMap>();

        public Guid? AudienceId { get; set; }
        public ICollection<Audience> Audiences { get; set; } = new List<Audience>();

        public string? VideoUrl { get; set; }
        public string? DocumentUrl { get; set; }
    }
}


