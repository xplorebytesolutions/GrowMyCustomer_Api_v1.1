using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CRM.Interfaces
{
    /// <summary>
    /// Defines the contract for all operations related to managing contacts.
    /// </summary>
    public interface IContactService
    {

        Task<ResponseResult> AddContactAsync(Guid businessId, ContactDto dto);
        Task<ContactDto> GetContactByIdAsync(Guid businessId, Guid contactId);
        Task<bool> UpdateContactAsync(Guid businessId, ContactDto dto);
        Task<bool> DeleteContactAsync(Guid businessId, Guid contactId);
        Task<CsvImportResult<ContactDto>> ParseCsvToContactsAsync(Guid businessId, Stream csvStream);
        Task<Contact> FindOrCreateAsync(Guid businessId, string phoneNumber);
        Task<bool> ToggleFavoriteAsync(Guid businessId, Guid contactId);
        Task<bool> ToggleArchiveAsync(Guid businessId, Guid contactId);
        Task<IEnumerable<ContactDto>> GetAllContactsAsync(Guid businessId, string? tab = "all");
        Task AssignTagToContactsAsync(Guid businessId, List<Guid> contactIds, Guid tagId);
        Task<PagedResult<ContactDto>> GetPagedContactsAsync(
             Guid businessId,
             string? tab = "all",
             int page = 1,
             int pageSize = 25,
             string? searchTerm = null
            );
        // ✅ Tag-based filtering support
        Task<IEnumerable<ContactDto>> GetContactsByTagsAsync(Guid businessId, List<Guid> tags);

        Task<BulkImportResultDto> BulkImportAsync(Guid businessId, Stream csvStream);
        // 📌 New method to support flow node → tag assignment
        Task<bool> AssignTagsAsync(Guid businessId, string phoneNumber, List<string> tags);

    }
}

