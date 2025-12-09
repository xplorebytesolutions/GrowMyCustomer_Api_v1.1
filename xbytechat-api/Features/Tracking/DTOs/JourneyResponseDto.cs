namespace xbytechat.api.Features.Tracking.DTOs
{
    public class JourneyResponseDto
    {
        public string CampaignType { get; set; } = "dynamic_url"; // or "flow"
        public string? FlowName { get; set; }
        public Guid? FlowId { get; set; }
        public Guid CampaignId { get; set; }
        public Guid ContactId { get; set; }
        public string ContactPhone { get; set; } = "";
        public List<JourneyEventDto> Events { get; set; } = new();
        public string? LeftOffAt { get; set; }  // step title or reason
    }
}
