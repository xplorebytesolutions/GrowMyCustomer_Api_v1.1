using System;

namespace xbytechat.api.Features.Automation.Models
{
    /// <summary>
    /// Context required to run an automation flow.
    /// </summary>
    public class FlowExecutionContext
    {
        public AutomationFlow Flow { get; set; }

        public Guid BusinessId { get; set; }

        public Guid ContactId { get; set; }

        public string ContactPhone { get; set; }

        public string SourceChannel { get; set; } = "manual";

        public string IndustryTag { get; set; } = "manual";
    }
}
