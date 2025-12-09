using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.DTOs.Messages
{
    public class BulkMessageDto
    {
        [Required]
        public List<Guid> ContactIds { get; set; } = new();

        [Required]
        public string MessageType { get; set; } = string.Empty; // "text" or "template"

        [Required]
        public string MessageTemplate { get; set; } = string.Empty;

        public string? TemplateName { get; set; }

        public List<string>? TemplateParams { get; set; }

        public DateTime? ScheduledAt { get; set; } // Optional future scheduling
    }
}
