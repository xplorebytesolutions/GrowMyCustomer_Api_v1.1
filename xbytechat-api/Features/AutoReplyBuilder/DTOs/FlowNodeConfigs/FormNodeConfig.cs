using System.Collections.Generic;
namespace xbytechat.api.Features.AutoReplyBuilder.DTOs.FlowNodeConfigs
{
    public class FormNodeConfig
    {
        public string Title { get; set; } = "Please fill out this form";

        public List<FormFieldConfig> Fields { get; set; } = new();

        public bool SaveToContact { get; set; } = true; // Whether to update contact info

        public string? SubmitMessage { get; set; } = "Thanks for submitting!";
    }

    public class FormFieldConfig
    {
        public string Key { get; set; } = string.Empty;      // contactName, email, phone
        public string Label { get; set; } = string.Empty;    // "Your Name"
        public string Type { get; set; } = "text";           // text, number, email, etc.
        public bool Required { get; set; } = true;
    }
}
