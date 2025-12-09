namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class FormInputNodeConfig
    {
        public string QuestionText { get; set; } = "Please enter your response:";

        public string FieldKey { get; set; } = "customer_name";
        // Used for storing user response under a label

        public string? ValidationRegex { get; set; }
        // Optional, e.g., @"^[0-9]{10}$" for phone numbers

        public string? PlaceholderHint { get; set; }
        // e.g., "Full Name", "10-digit Phone"

        public bool IsRequired { get; set; } = true;
    }
}
