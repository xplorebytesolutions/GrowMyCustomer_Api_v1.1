namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class ButtonChoiceNodeConfig
    {
        public string PromptText { get; set; } = "Please choose an option:";

        public List<ButtonOption> Options { get; set; } = new();
    }

    public class ButtonOption
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        // Optional metadata to control button behavior
        public string? NextStepHint { get; set; } // Can guide user or be used for logging
    }
}
