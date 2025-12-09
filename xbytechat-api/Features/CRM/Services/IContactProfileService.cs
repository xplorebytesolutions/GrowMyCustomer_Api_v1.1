using System;
using System.Threading;
using System.Threading.Tasks;

namespace xbytechat.api.Features.CRM.Services
{
    public interface IContactProfileService
    {
        /// <summary>
        /// Update contact's ProfileName if changed. Lookup by (BusinessId, E.164 phone).
        /// No-op if contact not found or name is empty.
        /// </summary>
        Task UpsertProfileNameAsync(Guid businessId, string phoneE164, string? profileName, CancellationToken ct = default);
    }
}
