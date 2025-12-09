namespace xbytechat.api.Features.TemplateModule.Models;

public sealed class TemplateDraftVariant
{
    public Guid Id { get; set; }
    public Guid TemplateDraftId { get; set; }
    public string Language { get; set; } = "en_US";
    public string BodyText { get; set; } = string.Empty;

    public string HeaderType { get; set; } = "NONE";             // TEXT|IMAGE|VIDEO|DOCUMENT|NONE
    public string? HeaderText { get; set; }
    public string? HeaderMediaLocalUrl { get; set; }

    public string? FooterText { get; set; }
    public string ButtonsJson { get; set; } = "[]";
    public string ExampleParamsJson { get; set; } = "{}";

    public bool IsReadyForSubmission { get; set; }
    public string? ValidationErrorsJson { get; set; }
}
