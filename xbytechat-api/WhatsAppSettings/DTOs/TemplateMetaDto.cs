using System.Collections.Generic;

namespace xbytechat.api.WhatsAppSettings.DTOs
{
    // Normalized snapshot of a provider template for FE + snapshots
    public sealed class TemplateMetaDto
    {
        public string Provider { get; set; } = "";     // "META_CLOUD" | "PINNACLE"
        public string TemplateId { get; set; } = "";   // provider’s id if you store it
        public string TemplateName { get; set; } = "";
        public string Language { get; set; } = "";     // e.g., "en_US"

        public bool HasHeaderMedia { get; set; }
        public string? HeaderType { get; set; }        // "IMAGE" | "VIDEO" | "DOCUMENT" (future)

        // 1-based placeholder slots for BODY
        public List<PlaceholderSlot> BodyPlaceholders { get; set; } = new();

        // Up to 3 buttons in template order
        public List<TemplateButtonMeta> Buttons { get; set; } = new();

    }

    public sealed class TemplateButtonMeta
    {
        // Provider-level type (e.g., "URL", "PHONE_NUMBER", "QUICK_REPLY")
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";         // label
        public string? Value { get; set; }             // ParameterValue from provider (may contain "{{1}}")
        public int Order { get; set; }                 // 0..2
    }

    public sealed class PlaceholderSlot
    {
        public int Index { get; set; }                 // 1..N
        public string? Label { get; set; }
        public string? Example { get; set; }
    }
}
