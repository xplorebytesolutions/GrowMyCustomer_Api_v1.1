using System.Collections.Generic;
using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat.api.Features.WhatsAppSettings.DTOs
{
    public sealed class TemplateSummaryResponseDto
    {
        public string TemplateId { get; init; } = "";
        public string Provider { get; init; } = "";
        public string Name { get; init; } = "";
        public string Language { get; init; } = "";

        public TemplateSummaryDto Summary { get; init; } = new();
    }
}
