using System.Threading.Tasks;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;

namespace xbytechat.api.Features.AutoReplyBuilder.Repositories
{
    public interface IAutoReplyLogRepository
    {
        Task SaveAsync(AutoReplyLogDto logDto);
    }
}
