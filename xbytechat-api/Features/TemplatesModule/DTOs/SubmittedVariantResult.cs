namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class SubmittedVariantResult
{
    public string Language { get; set; } = "en_US";
    public string Status { get; set; } = "PENDING";
    public string? RejectionReason { get; set; }
}
