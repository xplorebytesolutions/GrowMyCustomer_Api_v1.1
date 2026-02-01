namespace xbytechat.api.Features.TemplateModule.Models;

public sealed class TemplateDraft
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string Key { get; set; } = string.Empty;              // logical name (slug)
    public string Category { get; set; } = "UTILITY";            // MARKETING|UTILITY|AUTHENTICATION
    public string DefaultLanguage { get; set; } = "en_US";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }                   // When submitted to Meta (null = not submitted)
    public string? CreatedByUserId { get; set; }
}
