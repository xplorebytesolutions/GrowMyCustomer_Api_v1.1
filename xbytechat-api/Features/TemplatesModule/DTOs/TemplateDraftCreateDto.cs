namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class TemplateDraftCreateDto
{
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = "UTILITY";
    public string DefaultLanguage { get; set; } = "en_US";
}
