namespace xbytechat.api.Features.TemplateModule.DTOs;

public sealed class TemplatePreviewDto
{
    // High-level preview for UI
    public string Language { get; set; } = default!;
    public string Header { get; set; } = string.Empty;  // resolved header text (or “[IMAGE HEADER]”, etc.)
    public string Body { get; set; } = string.Empty;  // resolved with example params
    public string Footer { get; set; } = string.Empty;
    public List<string> Buttons { get; set; } = new();

    // Raw payload pieces your UI may also want
    public object ComponentsPayload { get; set; } = default!;  // as built by MetaComponentsBuilder
    public object ExamplesPayload { get; set; } = default!;  // as built by MetaComponentsBuilder
}
