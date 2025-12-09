using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CustomeApi.DTOs;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CustomeApi.Services
{
    public interface ICustomApiService
    {
        Task<ResponseResult> SendTemplateAsync(DirectTemplateSendRequest req, CancellationToken ct = default);
    }
}
