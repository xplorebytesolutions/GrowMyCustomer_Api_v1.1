namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class RescheduleDto
    {
        // must be UTC!
        public DateTime NewUtcTime { get; set; }
    }
}
