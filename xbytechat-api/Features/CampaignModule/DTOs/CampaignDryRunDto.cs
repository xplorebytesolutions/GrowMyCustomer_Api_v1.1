using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    public sealed class CampaignDryRunRecipientResultDto
    {
        public Guid? ContactId { get; set; }
        public string? ContactName { get; set; }
        public string PhoneNumber { get; set; } = "";
        public bool WouldSend { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        // Provider-shaped components (Meta/Pinnacle compatible)
        public List<object> ProviderComponents { get; set; } = new();
    }

    public sealed class CampaignDryRunResponseDto
    {
        public Guid CampaignId { get; set; }
        public string CampaignType { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string? Language { get; set; }
        public bool HasHeaderMedia { get; set; }
        public int RequiredPlaceholders { get; set; }
        public int ProvidedPlaceholders { get; set; }

        public int RecipientsConsidered { get; set; }
        public int WouldSendCount { get; set; }
        public int ErrorCount { get; set; }

        // Billability (best-effort estimate)
        public bool EstimatedChargeable { get; set; } = true;
        public string EstimatedConversationCategory { get; set; } = "template_outbound";
        public List<string> Notes { get; set; } = new();

        public List<CampaignDryRunRecipientResultDto> Results { get; set; } = new();
    }
}
