namespace xbytechat.api.Features.TemplateModule.Models;

public sealed class TemplateLibraryItem
{
    public Guid Id { get; set; }
    public string Industry { get; set; } = "RETAIL";
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = "UTILITY";
    public string? TagsJson { get; set; }                 // ["reminder","promo"]
    public bool IsFeatured { get; set; }
}
