using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace xbytechat.api.Features.ChatInbox.Services
{
    public interface IChatInboxMediaUploadService
    {
        Task<string> UploadToWhatsAppAsync(
            Guid businessId,
            string? phoneNumberId,
            IFormFile file,
            CancellationToken ct = default);
    }
}

