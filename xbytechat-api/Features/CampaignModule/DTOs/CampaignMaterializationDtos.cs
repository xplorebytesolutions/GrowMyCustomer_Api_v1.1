using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class ButtonResolutionDto
    {
        public string ButtonText { get; set; } = string.Empty;
        public string? RawTemplateValue { get; set; } // e.g. "https://x.com/{{1}}?q={{2}}"
        public string? ResolvedUrl { get; set; }
        public List<string> UsedPlaceholders { get; set; } = new(); // e.g. ["{{1}}","{{2}}"]
        public List<string> MissingArguments { get; set; } = new(); // e.g. ["{{2}}"]
        public List<string> Notes { get; set; } = new();
    }

    public class TemplateParamResolutionDto
    {
        public int Index { get; set; } // 1-based placeholder index from template ({{1}}, {{2}}, ...)
        public string? Value { get; set; }
        public string SourceType { get; set; } = string.Empty; // AudienceColumn | Static | Expression
        public string? SourceKey { get; set; } // column name when SourceType = AudienceColumn
        public bool IsMissing { get; set; }
        public string? Note { get; set; }
    }

    public class MaterializedRecipientDto
    {
        public Guid? RecipientId { get; set; }      // when using CampaignRecipients
        public Guid? ContactId { get; set; }
        public string? Phone { get; set; }

        public List<TemplateParamResolutionDto> Parameters { get; set; } = new();
        public List<ButtonResolutionDto> Buttons { get; set; } = new();

        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class CampaignMaterializeResultDto
    {
        public Guid CampaignId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int PlaceholderCount { get; set; }

        public int ReturnedCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        public List<MaterializedRecipientDto> Rows { get; set; } = new();
    }
}
