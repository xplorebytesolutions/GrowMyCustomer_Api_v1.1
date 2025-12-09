// Features/CampaignModule/DTOs/VideoTemplateMessageDto.cs
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class VideoTemplateMessageDto
    {
        public string RecipientNumber { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string LanguageCode { get; set; } = "en_US";

        // URL for the video header (HTTPS)
        public string? HeaderVideoUrl { get; set; }

        public List<string> TemplateParameters { get; set; } = new();
        public List<CampaignButtonDto> ButtonParameters { get; set; } = new();
    }
}
