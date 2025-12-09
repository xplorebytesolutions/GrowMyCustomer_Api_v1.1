// 📄 xbytechat-api/Features/CRM/Summary/Interfaces/IContactSummaryService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CRM.Summary.Dtos;

namespace xbytechat.api.Features.CRM.Summary.Interfaces
{
    /// <summary>
    /// Aggregates data from CRM modules (Contacts, Notes, Reminders, Timeline)
    /// into a single contact summary for the Chat Inbox / dashboards.
    /// </summary>
    public interface IContactSummaryService
    {
        /// <summary>
        /// Returns a compact CRM snapshot for the given contact and business.
        /// </summary>
        Task<ContactSummaryResponseDto?> GetContactSummaryAsync(
            Guid businessId,
            Guid contactId,
            CancellationToken ct = default);
    }
}
