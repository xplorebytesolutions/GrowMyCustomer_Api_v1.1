using System.Threading.Tasks;
using xbytechat.api.Features.MessageManagement.DTOs;

namespace xbytechat.api.Features.MessageManagement.Services
{
    public interface IMessageStatusService
    {
        Task LogWebhookStatusAsync(WebhookStatusDto dto);

    }
}
