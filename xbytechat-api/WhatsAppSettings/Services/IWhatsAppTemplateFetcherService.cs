using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.WhatsAppSettings.DTOs;


namespace xbytechat_api.WhatsAppSettings.Services
{
    public interface IWhatsAppTemplateFetcherService
    {
        Task<List<TemplateMetadataDto>> FetchTemplatesAsync(Guid businessId);
        Task<List<TemplateForUIResponseDto>> FetchAllTemplatesAsync();
        Task<TemplateMetadataDto?> GetTemplateByNameAsync(Guid businessId, string templateName, bool includeButtons);

        Task<IReadOnlyList<TemplateMetaDto>> GetTemplatesMetaAsync(Guid businessId, string? provider = null);
        Task<TemplateMetaDto?> GetTemplateMetaAsync(Guid businessId, string templateName, string? language = null, string? provider = null);

    }
}
