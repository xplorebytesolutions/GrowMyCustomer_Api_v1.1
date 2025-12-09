using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public class CampaignDryRunIssueDto
    {
        public Guid? RecipientId { get; set; }
        public Guid? ContactId { get; set; }
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// error | warn
        /// </summary>
        public string Severity { get; set; } = "error";

        public string Message { get; set; } = string.Empty;
    }

    public class CampaignDryRunResultDto
    {
        public Guid CampaignId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Placeholder count detected in the template (e.g., {{1}}, {{2}}, ...).
        /// </summary>
        public int PlaceholderCount { get; set; }

        public int CheckedRecipients { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        public List<CampaignDryRunIssueDto> Issues { get; set; } = new();
    }
}
