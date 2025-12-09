namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class FlowListItemDto
    {
        public Guid Id { get; set; }
        public string FlowName { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
    }
}
