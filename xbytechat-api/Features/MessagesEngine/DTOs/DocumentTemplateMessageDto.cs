using System;
using System.Collections.Generic;
using xbytechat.api.Features.CampaignModule.DTOs; // for CampaignButtonDto in this folder

namespace xbytechat.api.Features.CampaignModule.DTOs
{
    /// <summary>
    /// Payload for sending a template with a DOCUMENT header.
    /// Aliases provided so code using Parameters/Buttons OR TemplateParameters/ButtonParameters compiles.
    /// </summary>
    public sealed class DocumentTemplateMessageDto
    {
        public Guid BusinessId { get; set; }

        // Routing / provider
        public string? Provider { get; set; }            // "META" | "PINNACLE"
        public string? PhoneNumberId { get; set; }       // Meta WABA phone id (sender)

        // Recipient & template identity
        public string RecipientNumber { get; set; } = ""; // E.164
        public string TemplateName { get; set; } = "";
        public string LanguageCode { get; set; } = "en_US";

        // Header
        public string? HeaderDocumentUrl { get; set; }

        // Body params (ordered {{1}}..)
        public List<string> Parameters { get; set; } = new();
        // Alias for older call sites
        public List<string> TemplateParameters
        {
            get => Parameters;
            set => Parameters = value ?? new List<string>();
        }

        // Buttons (we use your actual CampaignButtonDto: ButtonText, ButtonType, TargetUrl)
        public List<CampaignButtonDto> Buttons { get; set; } = new();
        // Alias for older call sites
        public List<CampaignButtonDto> ButtonParameters
        {
            get => Buttons;
            set => Buttons = value ?? new List<CampaignButtonDto>();
        }

        // Optional extras
        public Guid? CTAFlowConfigId { get; set; }
        public string? TemplateBody { get; set; }
    }
}
