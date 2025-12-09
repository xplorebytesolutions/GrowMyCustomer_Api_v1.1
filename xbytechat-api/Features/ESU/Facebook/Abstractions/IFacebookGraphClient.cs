#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.ESU.Facebook.Abstractions
{
    public interface IFacebookGraphClient
    {
        Task<T> GetAsync<T>(
            Guid businessId,
            string path,
            IDictionary<string, string?>? query = null,
            CancellationToken ct = default);
    }
}
