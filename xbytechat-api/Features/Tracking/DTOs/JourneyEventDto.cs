namespace xbytechat.api.Features.Tracking.DTOs
{
    public class JourneyEventDto
    {
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = "System"; // System/User/Provider
        public string EventType { get; set; } = "";    // MessageSent/Delivered/Read/ButtonClicked/FlowStep/FlowSend/Redirect/Error
        public string Title { get; set; } = "";
        public string Details { get; set; } = "";
        public Guid? StepId { get; set; }
        public string? StepName { get; set; }
        public int? ButtonIndex { get; set; }
        public string? ButtonTitle { get; set; }
        public string? Url { get; set; }
        public string? TemplateName { get; set; }
    }
}
