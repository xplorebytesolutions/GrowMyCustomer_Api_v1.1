    namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class FlowNodeDto
    {
        public string Id { get; set; } = string.Empty;

        public string TemplateName { get; set; } = string.Empty;
        public string? TemplateType { get; set; } // ✅ e.g., "image_template", "text_template"

        // For templates with a media header (image/video/document), this URL is required at runtime.
        public string? HeaderMediaUrl { get; set; }

        // Optional static values for BODY placeholders ({{1}},{{2}},...). Stored as a list where index 0 => {{1}}.
        public List<string> BodyParams { get; set; } = new();

        // Optional static values for dynamic URL button params. Index 0 => button index "0" (position 1).
        // Only used when the template has URL buttons with ParameterValue like "https://.../{{1}}".
        public List<string> UrlButtonParams { get; set; } = new();

        public string MessageBody { get; set; } = string.Empty;
        public string? TriggerButtonText { get; set; }
        public string? TriggerButtonType { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }

        public string? RequiredTag { get; set; }         
        public string? RequiredSource { get; set; }      
        public List<LinkButtonDto> Buttons { get; set; } = new();
        public bool UseProfileName { get; set; }
        public int? ProfileNameSlot { get; set; }
        //(for flow trigger mapping)
        // ✅ NEW: ReactFlow expects this structure
        public PositionDto Position => new PositionDto
        {
            x = PositionX,
            y = PositionY
        };
        public class PositionDto
        {
            public float x { get; set; }
            public float y { get; set; }
        }
    }
}

