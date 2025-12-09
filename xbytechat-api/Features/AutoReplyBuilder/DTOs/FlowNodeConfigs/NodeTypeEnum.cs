namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class NodeTypeEnum
    {
        public const string Message = "message";
        public const string Template = "template";
        public const string Tag = "tag";
        public const string Wait = "wait";
        public const string ButtonChoice = "button_choice";
        public const string Branch = "branch";
        public const string End = "end"; // Optional: Used for flow exit
        public const string AgentHandoff = "agent_handoff"; // Optional: Transfer to human
    }
}
