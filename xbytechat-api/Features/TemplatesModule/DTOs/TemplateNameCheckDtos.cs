namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class TemplateNameCheckResponse
{
    public Guid DraftId { get; set; }
    public string Language { get; set; } = "en_US";

    public string Name { get; set; } = default!;       // computed name we intend to use
    public bool Available { get; set; }                // true if no collision in WhatsAppTemplates
    public string? Suggestion { get; set; }            // first available suggestion, if not available
}
