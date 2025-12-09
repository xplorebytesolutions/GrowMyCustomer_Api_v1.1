namespace xbytechat.api.Features.Automation.DTOs
{
    public class AutomationTriggerRequest
    {
        public string Keyword { get; set; }

        public string Phone { get; set; }

        public string? SourceChannel { get; set; }

        public string? IndustryTag { get; set; }
    }
}
