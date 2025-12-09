using System.Threading.Tasks;
using xbytechat.api.Features.Webhooks.DTOs;
using xbytechat.api.Features.Webhooks.Models;

namespace xbytechat.api.Features.Webhooks.Services
{
    public interface IFailedWebhookLogService
    {
        Task LogFailureAsync(FailedWebhookLogDto dto);
        Task<List<FailedWebhookLog>> GetAllAsync();
        Task<FailedWebhookLog?> GetByIdAsync(Guid id);
    }
}
