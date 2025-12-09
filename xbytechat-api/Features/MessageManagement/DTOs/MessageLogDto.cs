using System;

namespace xbytechat.api.Features.MessageManagement.DTOs
{
    public class MessageLogDto
    {
        public Guid Id { get; set; }
        public Guid? ContactId { get; set; }
        public string RecipientNumber { get; set; }
        public string MessageContent { get; set; }
        public bool IsIncoming { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RenderedBody { get; set; }
        public Guid? CampaignId { get; set; }
        public string? CampaignName { get; set; }
        public Guid? CTAFlowStepId { get; set; }
        public Guid? CTAFlowConfigId { get; set; }
    }
}

