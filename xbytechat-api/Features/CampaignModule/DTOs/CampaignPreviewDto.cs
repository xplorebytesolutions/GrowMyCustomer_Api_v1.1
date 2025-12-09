using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignPreviewRequestDto
    {
        // Optional: preview using a specific recipient’s contact info
        public Guid? ContactId { get; set; }
    }

    public class CampaignPreviewResponseDto
    {
        public Guid CampaignId { get; set; }
        public string TemplateName { get; set; } = "";
        public string Language { get; set; } = "en_US";
        public int PlaceholderCount { get; set; }

        public string BodyPreview { get; set; } = "";
        public List<string> MissingParams { get; set; } = new();  // e.g. ["{{2}} required but not supplied"]

        public bool HasHeaderMedia { get; set; }
        public string? HeaderType { get; set; } // IMAGE/VIDEO/DOCUMENT (if you later persist)

        public List<ButtonPreviewDto> Buttons { get; set; } = new();
    }

    public class ButtonPreviewDto
    {
        public int Index { get; set; }            // 0..2
        public string Text { get; set; } = "";
        public string Type { get; set; } = "URL"; // Meta types
        public bool IsDynamic { get; set; }       // needs parameter
        public string? TemplateParamBase { get; set; } // e.g. "/r/{{1}}"
        public string? CampaignValue { get; set; } // what user set in campaign (for dynamic)
        public string? TokenParam { get; set; }    // what we’d send when base has {{1}}
        public string? FinalUrlPreview { get; set; } // full tracked URL preview
    }
}
