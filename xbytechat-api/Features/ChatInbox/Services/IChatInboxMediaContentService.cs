using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.ChatInbox.Services
{
    public interface IChatInboxMediaContentService
    {
        Task<(Stream Stream, string ContentType)> DownloadFromWhatsAppAsync(
            Guid businessId,
            string mediaId,
            CancellationToken ct = default);
    }
}

