namespace xbytechat.api.Features.TemplateModule.Models;

public sealed class TemplateLibraryVariant
{
    public Guid Id { get; set; }
    public Guid LibraryItemId { get; set; }
    public string Language { get; set; } = "en_US";
    public string BodyText { get; set; } = string.Empty;

    public string HeaderType { get; set; } = "NONE";
    public string? HeaderText { get; set; }
    public string? HeaderDemoUrl { get; set; }
    public string? FooterText { get; set; }

    public string ButtonsJson { get; set; } = "[]";
    public string ExampleParamsJson { get; set; } = "{}";
}
