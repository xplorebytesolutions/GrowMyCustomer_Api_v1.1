using System;
using System.Collections.Generic;
using xbytechat.api.Features.CampaignModule.DTOs;

public class VideoTemplateMessageDto
{
    public Guid BusinessId { get; set; }
    public string RecipientNumber { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en_US";

    // mirrors HeaderImageUrl
    public string? HeaderVideoUrl { get; set; }

    public List<string> TemplateParameters { get; set; } = new();
    public List<CampaignButtonDto> ButtonParameters { get; set; } = new();

    // for flow tracking parity
    public Guid? CTAFlowConfigId { get; set; }
    public Guid? CTAFlowStepId { get; set; }
    public string? TemplateBody { get; set; }

    // same explicit provider knobs you already use
    public string Provider { get; set; } = string.Empty; // "PINNACLE" | "META_CLOUD"
    public string? PhoneNumberId { get; set; }
}
