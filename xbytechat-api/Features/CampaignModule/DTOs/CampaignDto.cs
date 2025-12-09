using System;
using System.Collections.Generic;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string MessageTemplate { get; set; }

        public string? TemplateId { get; set; }
        public string? MessageBody { get; set; }
        public string? CampaignType { get; set; }

        public string? Status { get; set; }

        public string? ImageUrl { get; set; }

        public string? ImageCaption { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ScheduledAt { get; set; }

        public Guid? CtaId { get; set; }

        public CtaPreviewDto? Cta { get; set; }

        public List<CampaignButtonDto> MultiButtons { get; set; } = new();

        public Guid? CTAFlowConfigId { get; set; }
        public string? CTAFlowName { get; set; }
    }

    // 📦 Embedded DTO for CTA preview (title + button text only)
    public class CtaPreviewDto
    {
        public string Title { get; set; } = string.Empty;

        public string ButtonText { get; set; } = string.Empty;
    }
}
