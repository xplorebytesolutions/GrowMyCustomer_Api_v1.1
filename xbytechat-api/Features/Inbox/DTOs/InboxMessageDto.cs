// 📄 File: Features/Inbox/DTOs/InboxMessageDto.cs
using System;

namespace xbytechat.api.Features.Inbox.DTOs
{
    public class InboxMessageDto
    {
        public Guid BusinessId { get; set; }
        public string RecipientPhone { get; set; }
        public string MessageBody { get; set; }
        public Guid? ContactId { get; set; }
        public Guid? CTAFlowStepId { get; set; }
        public Guid? CTAFlowConfigId { get; set; }
        public Guid? CampaignId { get; set; }
        public string? CampaignName { get; set; }       // 🆕 To show in chat bubble
        public string? RenderedBody { get; set; }

        public bool IsIncoming { get; set; }            // 🆕 Needed for bubble side
        public string Status { get; set; }              // 🆕 For message ticks
        public DateTime SentAt { get; set; }            // 🆕 For timestamp

        // ✅ NEW: Provider/WAMID idempotency key (Meta "messages[0].id")
        public string? ProviderMessageId { get; set; }
    }
}
