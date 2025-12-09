namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class TemplateLibraryListItemDto
{
    public Guid Id { get; set; }
    public string Industry { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Category { get; set; } = default!;
    public bool IsFeatured { get; set; }

    // UI helpers (from first variant or a representative one)
    public string Language { get; set; } = default!;
    public string HeaderType { get; set; } = "NONE";
    public int Placeholders { get; set; }       // {{n}} count in body
    public string BodyPreview { get; set; } = ""; // first 120 chars (no examples applied)
    public string ButtonsSummary { get; set; } = ""; // e.g., "URL, QUICK_REPLY"
}

public sealed class TemplateLibraryListResponse
{
    public bool Success { get; set; } = true;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public List<TemplateLibraryListItemDto> Items { get; set; } = new();
}
