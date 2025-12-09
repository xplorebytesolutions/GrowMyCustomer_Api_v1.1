using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignVariableMapDto
    {
        public Guid CampaignId { get; set; }
        public List<CampaignVariableMapItemDto> Items { get; set; } = new();
    }

    public class CampaignVariableMapItemDto
    {
        // Matches your normalized model fields
        public string Component { get; set; } = "";   // "body", "header", "button:url:1"
        public int Index { get; set; }                // 1..N

        public string SourceType { get; set; } = "Static"; // ContactField | CsvColumn | Static | Expression
        public string? SourceKey { get; set; }             // "name" / CSV header, etc.
        public string? StaticValue { get; set; }
        public string? Expression { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsRequired { get; set; } = false;
    }
}
