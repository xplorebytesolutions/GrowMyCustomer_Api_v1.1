using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.CTAManagement.DTOs;
using xbytechat.api.Features.MessagesEngine.DTOs; // Required to reference CTAButtonDto

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignCreateDto
    {
        public string Name { get; set; }

        [Column(TypeName = "text")]
        public string MessageTemplate { get; set; }

        public string? TemplateId { get; set; } // ✅ Optional Meta template ID

        public string? FollowUpTemplateId { get; set; } // 🔁 Auto-reply template after interest

        public string? CampaignType { get; set; } //= "template"; // "text", "template", "cta"

        public Guid? CtaId { get; set; } // 🔘 For legacy CTA support (optional)

        public Guid? CTAFlowConfigId { get; set; }
        public List<CampaignButtonDto> MultiButtons { get; set; } = new(); // ✅ New multi-button support
        public DateTime? ScheduledAt { get; set; } // 📅 Optional future scheduling

        //public List<Guid>? ContactIds { get; set; } // 👥 Target contact list

        public string? ImageUrl { get; set; } // 🖼️ Optional image field

        public string? ImageCaption { get; set; } // 📝 Optional caption

        public List<Guid> ContactIds { get; set; } = new();

        public List<string>? TemplateParameters { get; set; }
        public List<CampaignButtonParamFromMetaDto>? ButtonParams { get; set; }

        // Sender fields (REQUIRED for reliability)
        public string Provider { get; set; }            // "PINNACLE" | "META_CLOUD" (UPPERCASE)
        public string PhoneNumberId { get; set; }

        public string? HeaderKind { get; set; }        // "image" | "video" | "document" | "text" | "none"
        public string? HeaderMediaUrl { get; set; }
    }
}
