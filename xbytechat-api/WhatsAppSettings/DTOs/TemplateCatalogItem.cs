// xbytechat.api/WhatsAppSettings/DTOs/TemplateCatalogItem.cs
namespace xbytechat.api.WhatsAppSettings.DTOs
{
   

 
    public sealed record TemplateButtonMetadataDto
    {
        public string Text { get; init; } = "";
        public string Type { get; init; } = "";     // META raw: URL / QUICK_REPLY / PHONE_NUMBER / ...
        public string SubType { get; init; } = "";  // url / quick_reply / voice_call / ...
        public int Index { get; init; }             // position in the button list
        public string ParameterValue { get; init; } = "";
    }
}
