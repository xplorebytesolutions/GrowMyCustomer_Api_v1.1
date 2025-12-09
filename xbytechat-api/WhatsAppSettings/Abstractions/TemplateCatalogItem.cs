using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat.api.WhatsAppSettings.Abstractions
{
    public record TemplateCatalogItem(
       string Name,
       string Language,
       string Body,
       int PlaceholderCount,
       bool HasImageHeader,
       IReadOnlyList<ButtonMetadataDto> Buttons,
       string Status,
       string? Category,
       string? ExternalId,
       string RawJson,
        string? SubCategory = null
   );
}

