    namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public class FlowNodeDto
    {
        public string Id { get; set; } = string.Empty;

        public string TemplateName { get; set; } = string.Empty;
        public string? TemplateType { get; set; } // ✅ e.g., "image_template", "text_template"
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

