using System;
using System.Collections.Generic;

namespace xbytechat.api.WhatsAppSettings.DTOs
{
    public sealed class TemplatePreviewRequestDto
    {
        public string TemplateName { get; set; } = "";
        public string? Provider { get; set; }          // "META_CLOUD" | "PINNACLE" (optional)
        public string? Language { get; set; }          // e.g., "en_US" (optional)
        public string? HeaderImageUrl { get; set; }    // for image-header previews
        public List<string> TemplateParameters { get; set; } = new();  // BODY params in order
        public List<PreviewButtonInputDto> Buttons { get; set; } = new(); // up to 3
    }

    public sealed class PreviewButtonInputDto
    {
        public int Position { get; set; }              // 1..3 aligns with template button order
        public string? Type { get; set; }              // e.g., "URL"
        public string? Title { get; set; }             // label shown to user (optional)
        public string? Value { get; set; }             // for dynamic URL value or tel/wa deep link
    }

    public sealed class TemplatePreviewResponseDto
    {
        public bool FoundTemplate { get; set; }
        public string TemplateName { get; set; } = "";
        public string? Language { get; set; }
        public bool HasHeaderMedia { get; set; }
        public string HeaderType { get; set; } = "";   // "IMAGE", "VIDEO", etc.

        public int RequiredPlaceholderCount { get; set; }
        public int ProvidedPlaceholderCount { get; set; }
        public List<int> MissingPlaceholderIndices { get; set; } = new(); // 1-based
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        // A provider-shaped preview of what we'd send (Meta/Pinnacle use the same component shape)
        // Example:
        // [
        //   { type:"header", parameters:[{ type:"image", image:{ link:"..."}}]},
        //   { type:"body", parameters:[{type:"text",text:".."}, ...] },
        //   { type:"button", sub_type:"url", index:"0", parameters:[{ type:"text", text:"TOKEN_OR_URL"}] }
        // ]
        public List<object> ProviderComponentsPreview { get; set; } = new();
    }
}
