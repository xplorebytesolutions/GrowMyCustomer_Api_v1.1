namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class ButtonDto
{
    public string Type { get; set; } = "QUICK_REPLY";  // QUICK_REPLY|URL|PHONE
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Phone { get; set; }
}
