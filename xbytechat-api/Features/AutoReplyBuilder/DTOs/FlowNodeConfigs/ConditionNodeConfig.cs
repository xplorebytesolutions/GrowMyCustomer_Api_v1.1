using System.Collections.Generic;
namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class ConditionNodeConfig
    {
    
        public string InputKey { get; set; } = "buttonText";

        public Dictionary<string, string> PathMap { get; set; } = new();
       
    }
}
