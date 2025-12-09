namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class ForwardToAgentNodeConfig
    {
        public string? NoteToAgent { get; set; } // Optional instruction for agent
        public bool MarkAsUrgent { get; set; } = false;
    }
}
