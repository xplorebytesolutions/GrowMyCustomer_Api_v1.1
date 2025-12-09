namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class TemplateDraftVariantUpsertDto
{
    public string Language { get; set; } = "en_US";
    public string BodyText { get; set; } = string.Empty;

    public string HeaderType { get; set; } = "NONE";
    public string? HeaderText { get; set; }
    public string? HeaderMediaLocalUrl { get; set; }

    public string? FooterText { get; set; }
    public List<ButtonDto> Buttons { get; set; } = new();
    public Dictionary<string, string> Examples { get; set; } = new(); // e.g., {"1":"John","2":"12345"}
}
