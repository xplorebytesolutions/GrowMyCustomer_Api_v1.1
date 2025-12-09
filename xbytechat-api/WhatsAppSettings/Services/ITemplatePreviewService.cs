using System;
using System.Threading.Tasks;
using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat_api.WhatsAppSettings.Services
{
    public interface ITemplatePreviewService
    {
        Task<TemplatePreviewResponseDto> PreviewAsync(Guid businessId, TemplatePreviewRequestDto request);
    }
}
