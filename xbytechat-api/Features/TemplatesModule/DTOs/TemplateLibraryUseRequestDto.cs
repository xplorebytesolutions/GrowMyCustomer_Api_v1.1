namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class TemplateLibraryUseRequestDto
{
    public Guid LibraryItemId { get; set; }
    public List<string> Languages { get; set; } = new();
}
