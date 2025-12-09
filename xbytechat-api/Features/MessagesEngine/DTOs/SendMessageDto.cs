using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.MessagesEngine.DTOs.Validation;

namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    [ValidateMessageDto] // ✅ Custom validator will enforce conditional field rules
    public class SendMessageDto
    {
        [Required]
        public Guid BusinessId { get; set; }

        [Required]
        [Phone]
        public string RecipientNumber { get; set; } = string.Empty;

        [Required]
        public MessageTypeEnum MessageType { get; set; }

        // 📝 Text Message
        public string? TextContent { get; set; }

        // 🖼️ Image Message
        public string? MediaUrl { get; set; }

        // 📋 Template Message
        public string? TemplateName { get; set; }
        public Dictionary<string, string>? TemplateParameters { get; set; }

        // 🛒 CTA Message
        public List<CtaButtonDto>? CtaButtons { get; set; }

        // ✅ Required: this was missing [Optional but needed for CTA/Template message sending]
        public List<string>? ButtonParams { get; set; }

        // 📊 Optional Metadata
        public Guid? CampaignId { get; set; }
        public Guid? CTAFlowConfigId { get; set; }
        public Guid? CTAFlowStepId { get; set; }

        public string? SourceModule { get; set; }
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? BotId { get; set; }
        public string? RefMessageId { get; set; }
        public string? CTATriggeredFrom { get; set; }
        public DateTime? ScheduledAt { get; set; }

        // ✅ Add these two for flow tracking
        public string? TemplateBody { get; set; }  // 🔥 Used to render actual message body from placeholders

      //  [RegularExpression("^(PINNACLE|META_CLOUD)$")]
        public string Provider { get; set; } = string.Empty;

        public string? PhoneNumberId { get; set; }
    }
}
