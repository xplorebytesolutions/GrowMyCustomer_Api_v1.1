#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ESU.Facebook.DTOs;

namespace xbytechat.api.Features.ESU.Facebook.Abstractions
{
    public interface IEsuStatusService
    {
        Task<EsuStatusDto> GetStatusAsync(Guid businessId, CancellationToken ct = default);
        Task DeauthorizeAsync(Guid businessId, CancellationToken ct = default);
    }
}
