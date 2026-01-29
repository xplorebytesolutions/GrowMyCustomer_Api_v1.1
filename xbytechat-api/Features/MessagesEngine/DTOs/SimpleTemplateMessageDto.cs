using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.MessagesEngine.Enums;

namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    public class SimpleTemplateMessageDto
    {
        //public Guid BusinessId { get; set; }

        public string RecipientNumber { get; set; }

        public string TemplateName { get; set; }

        public List<string> TemplateParameters { get; set; } = new();
        public bool HasStaticButtons { get; set; } = false;

        // Optional: media header support (image/video/document)
        // HeaderKind is canonical lowercase: none | image | video | document | text
        public string? HeaderKind { get; set; }
        public string? HeaderMediaUrl { get; set; }

        // Optional: dynamic URL button params (index 0..2). Send only non-empty values.
        public List<string> UrlButtonParams { get; set; } = new();

       // [RegularExpression("^(PINNACLE|META_CLOUD)$")]
        public string Provider { get; set; } = string.Empty;
        public string? PhoneNumberId { get; set; }
        // ✅ Add these two for flow tracking
        public Guid? CTAFlowConfigId { get; set; }
        public Guid? CTAFlowStepId { get; set; }
        public string? TemplateBody { get; set; }  // 🔥 Used to render actual message body from placeholders

        public string? LanguageCode { get; set; }
        public DeliveryMode DeliveryMode { get; set; } = DeliveryMode.Queue;
    }
}

