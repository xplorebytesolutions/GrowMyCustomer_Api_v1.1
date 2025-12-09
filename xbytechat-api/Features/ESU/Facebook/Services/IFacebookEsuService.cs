using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ESU.Facebook.DTOs;

namespace xbytechat.api.Features.ESU.Facebook.Services
{
    public interface IFacebookEsuService
    {
        Task<FacebookEsuStartResponseDto> StartAsync(Guid businessId, string? returnUrl, CancellationToken ct = default);
        Task<FacebookEsuCallbackResponseDto> HandleCallbackAsync(string code, string state, CancellationToken ct = default);
        Task DisconnectAsync(Guid businessId, CancellationToken ct = default);
        Task FullDeleteAsync(Guid businessId, CancellationToken ct = default);

    }
}
