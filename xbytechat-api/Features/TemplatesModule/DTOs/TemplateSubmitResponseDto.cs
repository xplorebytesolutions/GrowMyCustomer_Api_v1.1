namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class TemplateSubmitResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SubmittedVariantResult> Variants { get; set; } = new();
}
