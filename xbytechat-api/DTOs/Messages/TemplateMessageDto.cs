using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.DTOs.Messages
{
    /// <summary>
    /// DTO for sending WhatsApp template-based messages.
    /// </summary>
    public class TemplateMessageDto : BaseMessageDto
    {
        [Required]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        public string LanguageCode { get; set; } = "en_US";

        public Dictionary<string, string> TemplateParameters { get; set; }

        public List<ButtonPayloadDto>? ButtonParams { get; set; } // ✅ NEW

        public override string MessageContent { get; set; } = "[Template]";
    }

    /// <summary>
    /// DTO for each button in a WhatsApp template.
    /// </summary>
    public class ButtonPayloadDto
    {
        public string SubType { get; set; } = "url"; // or "phone_number"
        public string Index { get; set; } = "0";      // 0-based index as string
        public string Param { get; set; } = string.Empty; // dynamic value for URL or phone number
    }
}
