using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ESU.Facebook.Models;

public interface IEsuTokenStore
{
    Task<EsuToken?> GetAsync(Guid businessId, string provider, CancellationToken ct);
    Task UpsertAsync(Guid businessId, string provider, string token, DateTime? expiresAtUtc, CancellationToken ct);
    Task RevokeAsync(Guid businessId, string provider, CancellationToken ct);

    Task DeleteAsync(Guid biz, string provider, CancellationToken ct = default);
}
