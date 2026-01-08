
using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.TemplateModule.DTOs;

public class DraftListItemDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = "en_US";
    public DateTime UpdatedAt { get; set; }
    public List<ComponentItemDto> Components { get; set; } = new();
}

public class ComponentItemDto
{
    public string Type { get; set; } = "BODY";
    public string? Text { get; set; }
    public string? Format { get; set; }
}
