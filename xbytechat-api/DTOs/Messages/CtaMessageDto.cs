using System;
using System.Collections.Generic;

namespace xbytechat.api.DTOs.Messages
{
    public class CtaMessageDto
    {
        // 🎯 Required Fields
        public string RecipientPhone { get; set; } = string.Empty;
        public string BodyText { get; set; } = string.Empty;
        public List<string> Buttons { get; set; } = new();

        // 🔗 Optional Source Info
        public Guid? BusinessId { get; set; }          // Optional: Track for MessageLog
        public Guid? CampaignId { get; set; }          // Optional: If triggered via Campaign
        public string? SourceModule { get; set; }      // e.g., "Catalog", "CRM", "Timeline"

        // 👤 Customer Context (Optional Enrichment)
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }

        // 📦 Advanced (Optional but useful)
        public string? BotId { get; set; }             // Bot which served this (optional)
        public string? RefMessageId { get; set; }      // Link to previous message (thread)
        public string? CTATriggeredFrom { get; set; }  // e.g., “Buy Now”, “Know More”

        // ⏱️ Timestamps / Meta
        public DateTime? ScheduledAt { get; set; }     // For future automation (optional)
    }
}
