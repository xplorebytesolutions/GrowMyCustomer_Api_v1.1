using System;

namespace xbytechat.api.Features.Automation.Models
{
    public class AutomationFlowNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Label { get; set; } = string.Empty;
        public NodeTypeEnum NodeType { get; set; }
        public string ConfigJson { get; set; } = "{}";
    }
}

