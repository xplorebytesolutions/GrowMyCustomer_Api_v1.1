namespace xbytechat.api.Features.TemplatesModule.DTOs
{
    // e.g. in the same file or a shared DTOs folder
    public sealed class ButtonMetadataDto
    {
        public string Text { get; set; } = "";
        public string Type { get; set; } = "";     // e.g. "QUICK_REPLY", "URL", "COPY_CODE", "FLOW"
        public string SubType { get; set; } = "";  // e.g. "url", "copy_code", "flow" (lowercase for UI)
        public string? ParameterValue { get; set; } // URL template or static value (if any)
    }

}
