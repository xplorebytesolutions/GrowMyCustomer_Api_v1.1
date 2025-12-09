namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class NodeIconMap
    {
        public static readonly Dictionary<string, string> IconMap = new()
        {
            { NodeTypeEnum.Message, "🗨️" },
            { NodeTypeEnum.Template, "📄" },
            { NodeTypeEnum.Tag, "🏷️" },
            { NodeTypeEnum.Wait, "⏱️" },
            { NodeTypeEnum.ButtonChoice, "🔘" },
            { NodeTypeEnum.Branch, "🌿" },
            { NodeTypeEnum.AgentHandoff, "👨‍💼" },
            { NodeTypeEnum.End, "⛔" }
        };
    }
}
