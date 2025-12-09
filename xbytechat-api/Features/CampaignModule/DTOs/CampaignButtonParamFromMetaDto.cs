namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignButtonParamFromMetaDto
    {
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int Position { get; set; }
    }
}
