namespace xbytechat.api.Features.CampaignTracking.DTOs
{
    public class TopCampaignDto
    {
        public Guid CampaignId { get; set; }
        public string CampaignName { get; set; }
        public double ReadRate { get; set; }
        public double ClickThroughRate { get; set; }
    }
}
