using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.DTOs.Messages
{
    public class ImageMessageDto : BaseMessageDto
    {
        [Required]
        public override string MessageContent { get; set; } = string.Empty;

        [Required]
        [Url]
        public string MediaUrl { get; set; } = string.Empty;
    }
}
