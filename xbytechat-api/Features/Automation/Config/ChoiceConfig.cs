namespace xbytechat.api.Features.Automation.Config
{
    public class ChoiceConfig
    {
        public List<ChoiceCondition> Conditions { get; set; } = new();
        public string FallbackNodeId { get; set; }
    }

    public class ChoiceCondition
    {
        public string Match { get; set; }
        public string NextNodeId { get; set; }
    }
}
