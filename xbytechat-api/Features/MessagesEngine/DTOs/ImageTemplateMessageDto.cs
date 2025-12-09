using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.CampaignModule.DTOs;

public class ImageTemplateMessageDto
{
    public Guid BusinessId { get; set; }
    public string RecipientNumber { get; set; }
    public string TemplateName { get; set; }
    public string LanguageCode { get; set; } = "en_US";
    public string HeaderImageUrl { get; set; }
    public List<string> TemplateParameters { get; set; } = new();
    public List<CampaignButtonDto> ButtonParameters { get; set; } = new();

    // ✅ Add these two for flow tracking
    public Guid? CTAFlowConfigId { get; set; }
    public Guid? CTAFlowStepId { get; set; }
    public string? TemplateBody { get; set; }


   // [RegularExpression("^(PINNACLE|META_CLOUD)$")]
    public string Provider { get; set; } = string.Empty;
    public string? PhoneNumberId { get; set; }
   

}
