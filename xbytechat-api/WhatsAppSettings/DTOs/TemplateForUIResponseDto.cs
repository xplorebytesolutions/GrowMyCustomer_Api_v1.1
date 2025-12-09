namespace xbytechat.api.WhatsAppSettings.DTOs
{
    public class TemplateForUIResponseDto
    {
        public string Name { get; set; } = "";
        public string Language { get; set; } = "en_US";
        public string Body { get; set; } = "";
        public int ParametersCount { get; set; }
        public bool HasImageHeader { get; set; }
        public List<ButtonMetadataDto> ButtonParams { get; set; } = new();
    }
}
