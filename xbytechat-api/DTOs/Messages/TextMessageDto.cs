using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.DTOs.Messages
{
    public class TextMessageDto : BaseMessageDto
    {
        [Required]
        public override string MessageContent { get; set; } = string.Empty;
    }
}
