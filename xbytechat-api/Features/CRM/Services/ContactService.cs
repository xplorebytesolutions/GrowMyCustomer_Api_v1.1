using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CRM.Services
{
    public class ContactService : IContactService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ContactService> _logger;

        public ContactService(AppDbContext db, ILogger<ContactService> logger)
        {
            _db = db;
            _logger = logger;
        }

        //public async Task<ContactDto> AddContactAsync(Guid businessId, ContactDto dto)
        //{
        //    _logger.LogInformation("AddContactAsync called for businessId={BusinessId}, Name={Name}", businessId, dto.Name);

        //    var contact = new Contact
        //    {
        //        Id = Guid.NewGuid(),
        //        BusinessId = businessId,
        //        Name = dto.Name,
        //        PhoneNumber = dto.PhoneNumber,
        //        Email = dto.Email,
        //        LeadSource = dto.LeadSource,
        //        LastContactedAt = dto.LastContactedAt?.ToUniversalTime(),
        //        NextFollowUpAt = dto.NextFollowUpAt?.ToUniversalTime(),
        //        Notes = dto.Notes,
        //        CreatedAt = DateTime.UtcNow,
        //        IsFavorite = dto.IsFavorite,
        //        IsArchived = dto.IsArchived,
        //        Group = dto.Group
        //    };

        //    if (dto.Tags != null && dto.Tags.Any())
        //    {
        //        contact.ContactTags = dto.Tags.Select(t => new ContactTag
        //        {
        //            Id = Guid.NewGuid(),
        //            ContactId = contact.Id,
        //            TagId = t.TagId,
        //            BusinessId = businessId,
        //            AssignedAt = DateTime.UtcNow,
        //            AssignedBy = "system"
        //        }).ToList();
        //    }

        //    _db.Contacts.Add(contact);

        //    try
        //    {
        //        await _db.SaveChangesAsync();
        //        _logger.LogInformation("Contact added: {ContactId} for businessId={BusinessId}", contact.Id, businessId);
        //    }
        //    catch (DbUpdateException ex)
        //    {
        //        _logger.LogError(ex, "DB error in AddContactAsync (Contact: {Contact}, BusinessId={BusinessId})", contact, businessId);
        //        var innerMessage = ex.InnerException?.Message ?? ex.Message;
        //        throw new Exception("❌ DB save error (Contact): " + innerMessage, ex);
        //    }

        //    return new ContactDto
        //    {
        //        Id = contact.Id,
        //        Name = contact.Name,
        //        PhoneNumber = contact.PhoneNumber,
        //        Email = contact.Email,
        //        LeadSource = contact.LeadSource,
        //        LastContactedAt = contact.LastContactedAt,
        //        NextFollowUpAt = contact.NextFollowUpAt,
        //        Notes = contact.Notes,
        //        CreatedAt = contact.CreatedAt,
        //        Tags = dto.Tags ?? new List<ContactTagDto>()
        //    };
        //}

        //public async Task<ResponseResult> AddContactAsync(Guid businessId, ContactDto dto)
        //{
        //    _logger.LogInformation("📩 AddContactAsync called for businessId={BusinessId}, Name={Name}", businessId, dto.Name);

        //    try
        //    {
        //        // 1. Duplicate check
        //        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        //        {
        //            var existingContact = await _db.Contacts.FirstOrDefaultAsync(c =>
        //                c.BusinessId == businessId && c.PhoneNumber == dto.PhoneNumber);

        //            if (existingContact != null)
        //            {
        //                _logger.LogWarning("⚠️ Duplicate contact attempt for phone {Phone}", dto.PhoneNumber);
        //                return ResponseResult.ErrorInfo(
        //                    $"❌ A contact with the phone number '{dto.PhoneNumber}' already exists."
        //                );
        //            }
        //        }

        //        // 2. Build entity
        //        var contact = new Contact
        //        {
        //            Id = Guid.NewGuid(),
        //            BusinessId = businessId,
        //            Name = dto.Name,
        //            PhoneNumber = dto.PhoneNumber,
        //            Email = dto.Email,
        //            LeadSource = dto.LeadSource,
        //            LastContactedAt = dto.LastContactedAt?.ToUniversalTime(),
        //            NextFollowUpAt = dto.NextFollowUpAt?.ToUniversalTime(),
        //            Notes = dto.Notes,
        //            CreatedAt = DateTime.UtcNow,
        //            IsFavorite = dto.IsFavorite,
        //            IsArchived = dto.IsArchived,
        //            Group = dto.Group
        //        };

        //        // 3. Tags mapping
        //        if (dto.Tags != null && dto.Tags.Any())
        //        {
        //            contact.ContactTags = dto.Tags.Select(t => new ContactTag
        //            {
        //                Id = Guid.NewGuid(),
        //                ContactId = contact.Id,
        //                TagId = t.TagId,
        //                BusinessId = businessId,
        //                AssignedAt = DateTime.UtcNow,
        //                AssignedBy = "system"
        //            }).ToList();
        //        }

        //        _db.Contacts.Add(contact);

        //        // 4. Save
        //        try
        //        {
        //            await _db.SaveChangesAsync();
        //            _logger.LogInformation("✅ Contact added successfully: {ContactId} (BusinessId={BusinessId})", contact.Id, businessId);
        //        }
        //        catch (DbUpdateException ex)
        //        {
        //            _logger.LogError(ex, "❌ DB error in AddContactAsync (BusinessId={BusinessId})", businessId);
        //            var innerMessage = ex.InnerException?.Message ?? ex.Message;
        //            return ResponseResult.ErrorInfo("❌ Database save error (Contact): " + innerMessage);
        //        }

        //        // 5. Map back to DTO
        //        var resultDto = new ContactDto
        //        {
        //            Id = contact.Id,
        //            Name = contact.Name,
        //            PhoneNumber = contact.PhoneNumber,
        //            Email = contact.Email,
        //            LeadSource = contact.LeadSource,
        //            LastContactedAt = contact.LastContactedAt,
        //            NextFollowUpAt = contact.NextFollowUpAt,
        //            Notes = contact.Notes,
        //            CreatedAt = contact.CreatedAt,
        //            IsFavorite = contact.IsFavorite,
        //            IsArchived = contact.IsArchived,
        //            Group = contact.Group,
        //            Tags = dto.Tags ?? new List<ContactTagDto>()
        //        };

        //        return ResponseResult.SuccessInfo("✅ Contact created successfully.", resultDto);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "🚨 Unexpected error in AddContactAsync (BusinessId={BusinessId})", businessId);
        //        return ResponseResult.ErrorInfo("🚨 A server error occurred while creating the contact.", ex.Message);
        //    }
        //}

        public async Task<ResponseResult> AddContactAsync(Guid businessId, ContactDto dto)
        {
            _logger.LogInformation("📩 AddContactAsync called for businessId={BusinessId}, Name={Name}", businessId, dto.Name);

            try
            {
                // 1. Normalize the phone number using your private method first.
                var normalizedPhone = NormalizePhone(dto.PhoneNumber);

                // 2. Validate the normalized number.
                // Your NormalizePhone method returns an empty string for invalid numbers.
                if (string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    return ResponseResult.ErrorInfo("❌ Phone number is invalid. It must contain exactly 10 digits.");
                }

                // 3. Use the clean, normalized number for the duplicate check.
                var existingContact = await _db.Contacts.FirstOrDefaultAsync(c =>
                    c.BusinessId == businessId && c.PhoneNumber == normalizedPhone);

                if (existingContact != null)
                {
                    _logger.LogWarning("⚠️ Duplicate contact attempt for phone {Phone}", dto.PhoneNumber);
                    return ResponseResult.ErrorInfo(
                        $"❌ A contact with the phone number '{dto.PhoneNumber}' already exists."
                    );
                }

                // 4. Build the new contact entity, SAVING the normalized number.
                var contact = new Contact
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = dto.Name,
                    PhoneNumber = normalizedPhone, // Save the standardized number
                    Email = dto.Email,
                    LeadSource = dto.LeadSource,
                    LastContactedAt = dto.LastContactedAt?.ToUniversalTime(),
                    NextFollowUpAt = dto.NextFollowUpAt?.ToUniversalTime(),
                    Notes = dto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    IsFavorite = dto.IsFavorite,
                    IsArchived = dto.IsArchived,
                    Group = dto.Group
                };

                // Map tags if they are provided
                if (dto.Tags != null && dto.Tags.Any())
                {
                    contact.ContactTags = dto.Tags.Select(t => new ContactTag
                    {
                        Id = Guid.NewGuid(),
                        ContactId = contact.Id,
                        TagId = t.TagId,
                        BusinessId = businessId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = "system"
                    }).ToList();
                }

                _db.Contacts.Add(contact);
                await _db.SaveChangesAsync();
                _logger.LogInformation("✅ Contact added successfully: {ContactId}", contact.Id);

                // Map the created entity back to a DTO for the response
                var resultDto = new ContactDto
                {
                    Id = contact.Id,
                    Name = contact.Name,
                    PhoneNumber = contact.PhoneNumber,
                    Email = contact.Email,
                    LeadSource = contact.LeadSource,
                    CreatedAt = contact.CreatedAt,
                    Tags = contact.ContactTags?.Select(ct => new ContactTagDto { TagId = ct.TagId }).ToList() ?? new List<ContactTagDto>()
                };

                return ResponseResult.SuccessInfo("✅ Contact created successfully.", resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚨 Unexpected error in AddContactAsync for business {BusinessId}", businessId);
                return ResponseResult.ErrorInfo("🚨 A server error occurred while creating the contact.", ex.Message);
            }
        }
        public async Task<ContactDto> GetContactByIdAsync(Guid businessId, Guid contactId)
        {
            _logger.LogInformation("GetContactByIdAsync: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
            try
            {
                var contact = await _db.Contacts
                     .Where(c => c.BusinessId == businessId && c.Id == contactId && c.IsActive)
                    .Include(c => c.ContactTags)
                        .ThenInclude(ct => ct.Tag)
                    .FirstOrDefaultAsync();

                if (contact == null)
                {
                    _logger.LogWarning("Contact not found: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                    return null;
                }

                return new ContactDto
                {
                    Id = contact.Id,
                    Name = contact.Name,
                    PhoneNumber = contact.PhoneNumber,
                    Email = contact.Email,
                    LeadSource = contact.LeadSource,
                    LastContactedAt = contact.LastContactedAt,
                    NextFollowUpAt = contact.NextFollowUpAt,
                    Notes = contact.Notes,
                    CreatedAt = contact.CreatedAt,
                    Tags = contact.ContactTags?
                        .Where(ct => ct.Tag != null)
                        .Select(ct => new ContactTagDto
                        {
                            TagId = ct.TagId,
                            TagName = ct.Tag.Name
                        })
                        .ToList() ?? new List<ContactTagDto>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching contact by id: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                throw;
            }
        }

        public async Task<bool> UpdateContactAsync(Guid businessId, ContactDto dto)
        {
            _logger.LogInformation("UpdateContactAsync: businessId={BusinessId}, contactId={ContactId}", businessId, dto.Id);
            try
            {
                var contact = await _db.Contacts
                    .Include(c => c.ContactTags)
                    .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == dto.Id);

                if (contact == null)
                {
                    _logger.LogWarning("Contact not found for update: businessId={BusinessId}, contactId={ContactId}", businessId, dto.Id);
                    return false;
                }

                contact.Name = dto.Name;
                contact.PhoneNumber = dto.PhoneNumber;
                contact.Email = dto.Email;
                contact.LeadSource = dto.LeadSource;
                contact.LastContactedAt = dto.LastContactedAt?.ToUniversalTime();
                contact.NextFollowUpAt = dto.NextFollowUpAt?.ToUniversalTime();
                contact.Notes = dto.Notes;

                await _db.SaveChangesAsync();
                _logger.LogInformation("Contact updated: {ContactId}", contact.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact: businessId={BusinessId}, contactId={ContactId}", businessId, dto.Id);
                throw;
            }
        }

        public async Task<bool> DeleteContactAsync(Guid businessId, Guid contactId)
        {
            _logger.LogInformation("DeleteContactAsync: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
            try
            {
                var contact = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId && c.IsActive);

                if (contact == null)
                {
                    _logger.LogWarning("Contact not found for delete: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                    return false;
                }

                contact.IsActive = false; // 👈 Soft delete
                await _db.SaveChangesAsync();
                _logger.LogInformation("Contact soft-deleted: {ContactId}", contactId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                throw;
            }
        }


        public async Task<CsvImportResult<ContactDto>> ParseCsvToContactsAsync(Guid businessId, Stream csvStream)
        {
            _logger.LogInformation("ParseCsvToContactsAsync: businessId={BusinessId}", businessId);

            var result = new CsvImportResult<ContactDto>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);

            // Register custom column mapping for ContactDto
            csv.Context.RegisterClassMap<ContactDtoCsvMap>();

            int rowNumber = 1;

            await csv.ReadAsync();     // Move to first row
            csv.ReadHeader();          // Read header row

            while (await csv.ReadAsync())
            {
                rowNumber++;
                try
                {
                    var record = csv.GetRecord<ContactDto>();
                    record.CreatedAt = DateTime.UtcNow;

                    result.SuccessRecords.Add(record);
                }
                catch (Exception ex)
                {
                    // Avoid ambiguity by using explicit object instantiation
                    var error = new CsvImportError
                    {
                        RowNumber = rowNumber,
                        ErrorMessage = ex.Message
                    };
                    result.Errors.Add(error);
                }
            }

            _logger.LogInformation("CSV parsed with {SuccessCount} successes and {ErrorCount} errors.",
                result.SuccessRecords.Count, result.Errors.Count);

            return result;
        }

        //private string NormalizePhone(string phoneNumber)
        //{
        //    if (string.IsNullOrWhiteSpace(phoneNumber))
        //        return phoneNumber;

        //    var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        //    // If it starts with "91" and length = 12 → add +
        //    if (digits.StartsWith("91") && digits.Length == 12)
        //        return "+" + digits;

        //    // If it starts with "91" and length = 10 (missing country code) → add +91
        //    if (digits.Length == 10)
        //        return "+91" + digits;

        //    // If it already includes country code with + (13 digits for India)
        //    if (digits.StartsWith("91") && digits.Length == 12)
        //        return "+" + digits;

        //    // Fallback → return with +
        //    if (!digits.StartsWith("+"))
        //        return "+" + digits;

        //    return digits;
        //}

        private string NormalizePhone(string phoneNumber)
        {
            // 1. Handle empty or null input
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return string.Empty;
            }

            // 2. Extract only the numeric digits from the string
            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

            // 3. If the number starts with India's country code (91) and is 12 digits long,
            //    strip the country code to get the core 10-digit number.
            if (digits.StartsWith("91") && digits.Length == 12)
            {
                digits = digits.Substring(2);
            }

            // 4. NEW: Strictly validate that the result is 10 digits long.
            if (digits.Length != 10)
            {
                // If the number of digits is not exactly 10, it's invalid.
                // Return an empty string to signal that it could not be normalized.
                return string.Empty;
            }

            // 5. If the number is a valid 10 digits, return it in the standard +91 format.
            return "+91" + digits;
        }
        public async Task<Contact> FindOrCreateAsync(Guid businessId, string phoneNumber)
        {
            var normalized = NormalizePhone(phoneNumber);
            _logger.LogInformation("FindOrCreateAsync: businessId={BusinessId}, rawPhone={PhoneNumber}, normalized={Normalized}",
                businessId, phoneNumber, normalized);

            try
            {
                var contact = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == normalized);

                if (contact != null)
                {
                    _logger.LogInformation("Contact already exists: contactId={ContactId}", contact.Id);
                    return contact;
                }

                var newContact = new Contact
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = "WhatsApp User",
                    PhoneNumber = normalized,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Contacts.Add(newContact);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Contact created: {ContactId}", newContact.Id);

                return newContact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FindOrCreateAsync: businessId={BusinessId}, phoneNumber={PhoneNumber}", businessId, phoneNumber);
                throw;
            }
        }

        public async Task<bool> ToggleFavoriteAsync(Guid businessId, Guid contactId)
        {
            _logger.LogInformation("ToggleFavoriteAsync: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
            try
            {
                var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId);
                if (contact == null)
                {
                    _logger.LogWarning("Contact not found for favorite toggle: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                    return false;
                }

                contact.IsFavorite = !contact.IsFavorite;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Contact favorite toggled: {ContactId} -> {IsFavorite}", contactId, contact.IsFavorite);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling favorite: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                throw;
            }
        }

        public async Task AssignTagToContactsAsync(Guid businessId, List<Guid> contactIds, Guid tagId)
        {
            _logger.LogInformation("AssignTagToContactsAsync: businessId={BusinessId}, tagId={TagId}, contactIds={ContactIds}", businessId, tagId, contactIds);
            try
            {
                var contacts = await _db.Contacts
                    .Where(c => c.BusinessId == businessId && contactIds.Contains(c.Id))
                    .Include(c => c.ContactTags)
                    .ToListAsync();

                foreach (var contact in contacts)
                {
                    bool alreadyAssigned = contact.ContactTags.Any(link => link.TagId == tagId);
                    if (!alreadyAssigned)
                    {
                        contact.ContactTags.Add(new ContactTag
                        {
                            ContactId = contact.Id,
                            TagId = tagId
                        });
                    }
                }
                await _db.SaveChangesAsync();
                _logger.LogInformation("Tags assigned to contacts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning tag: businessId={BusinessId}, tagId={TagId}", businessId, tagId);
                throw;
            }
        }

        public async Task<bool> ToggleArchiveAsync(Guid businessId, Guid contactId)
        {
            _logger.LogInformation("ToggleArchiveAsync: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
            try
            {
                var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId);
                if (contact == null)
                {
                    _logger.LogWarning("Contact not found for archive toggle: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                    return false;
                }

                contact.IsArchived = !contact.IsArchived;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Contact archive toggled: {ContactId} -> {IsArchived}", contactId, contact.IsArchived);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling archive: businessId={BusinessId}, contactId={ContactId}", businessId, contactId);
                throw;
            }
        }

        public async Task<IEnumerable<ContactDto>> GetAllContactsAsync(Guid businessId, string? tab = "all")
        {
            _logger.LogInformation("GetAllContactsAsync: businessId={BusinessId}, tab={Tab}", businessId, tab);
            try
            {
                var baseQuery = _db.Contacts
                    .Where(c => c.BusinessId == businessId && c.IsActive);

                if (tab == "favourites")
                    baseQuery = baseQuery.Where(c => c.IsFavorite);
                else if (tab == "archived")
                    baseQuery = baseQuery.Where(c => c.IsArchived);
                else if (tab == "groups")
                    baseQuery = baseQuery.Where(c => !string.IsNullOrEmpty(c.Group));

                var query = baseQuery
                    .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag);

                var contacts = await query.ToListAsync();

                var result = contacts.Select(c => new ContactDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                    LeadSource = c.LeadSource,
                    LastContactedAt = c.LastContactedAt,
                    NextFollowUpAt = c.NextFollowUpAt,
                    Notes = c.Notes,
                    CreatedAt = c.CreatedAt,
                    IsFavorite = c.IsFavorite,
                    IsArchived = c.IsArchived,
                    Group = c.Group,
                    Tags = c.ContactTags?
                        .Where(ct => ct.Tag != null)
                        .Select(ct => new ContactTagDto
                        {
                            TagId = ct.TagId,
                            TagName = ct.Tag.Name,
                            ColorHex = ct.Tag.ColorHex,
                            Category = ct.Tag.Category
                        })
                        .ToList() ?? new List<ContactTagDto>()
                });

                _logger.LogInformation("GetAllContactsAsync returned {Count} contacts", contacts.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllContactsAsync: businessId={BusinessId}", businessId);
                throw;
            }
        }
        public async Task<PagedResult<ContactDto>> GetPagedContactsAsync(Guid businessId, string? tab, int page, int pageSize, string? searchTerm)
        {
            _logger.LogInformation("GetPagedContactsAsync: businessId={BusinessId}, tab={Tab}, page={Page}, pageSize={PageSize}",
                businessId, tab, page, pageSize);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 100) pageSize = 100; // max limit

            var baseQuery = _db.Contacts
                .Where(c => c.BusinessId == businessId && c.IsActive);

            if (tab == "favourites")
                baseQuery = baseQuery.Where(c => c.IsFavorite);
            else if (tab == "archived")
                baseQuery = baseQuery.Where(c => c.IsArchived);
            else if (tab == "groups")
                baseQuery = baseQuery.Where(c => !string.IsNullOrEmpty(c.Group));

            var totalCount = await baseQuery.CountAsync();

            var contacts = await baseQuery
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .OrderBy(c => c.Name) // or any order preferred
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = contacts.Select(c => new ContactDto
            {
                Id = c.Id,
                Name = c.Name,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                LeadSource = c.LeadSource,
                LastContactedAt = c.LastContactedAt,
                NextFollowUpAt = c.NextFollowUpAt,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt,
                IsFavorite = c.IsFavorite,
                IsArchived = c.IsArchived,
                Group = c.Group,
                Tags = c.ContactTags?
                    .Where(ct => ct.Tag != null)
                    .Select(ct => new ContactTagDto
                    {
                        TagId = ct.TagId,
                        TagName = ct.Tag.Name,
                        ColorHex = ct.Tag.ColorHex,
                        Category = ct.Tag.Category
                    })
                    .ToList() ?? new List<ContactTagDto>()
            }).ToList();

            return new PagedResult<ContactDto>
            {
                Items = items,
                TotalCount = totalCount
            };
        }

        public async Task<IEnumerable<ContactDto>> GetContactsByTagsAsync(Guid businessId, List<string> tags)
        {
            var contacts = await _db.Contacts
                .Where(c => c.BusinessId == businessId && !c.IsArchived)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .Where(c => c.ContactTags.Any(ct => tags.Contains(ct.Tag.Name))) // 🔍 Filter by tag names
                .OrderBy(c => c.Name)
                .Select(c => new ContactDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                    Notes = c.Notes,
                    Tags = c.ContactTags.Select(ct => new ContactTagDto
                    {
                        TagId = ct.Tag.Id,
                        TagName = ct.Tag.Name,
                        ColorHex = ct.Tag.ColorHex,
                        Category = ct.Tag.Category
                    }).ToList()
                })
                .ToListAsync();

            return contacts;
        }
        public async Task<BulkImportResultDto> BulkImportAsync(Guid businessId, Stream csvStream)
        {
            _logger.LogInformation("Bulk import started for businessId={BusinessId}", businessId);

            var result = new BulkImportResultDto();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<ContactDtoCsvMap>();

            await csv.ReadAsync();
            csv.ReadHeader();

            var contactsToAdd = new List<Contact>();
            int row = 1;

            while (await csv.ReadAsync())
            {
                row++;
                try
                {
                    var dto = csv.GetRecord<ContactDto>();
                    if (string.IsNullOrWhiteSpace(dto.PhoneNumber)) continue;

                    var contact = new Contact
                    {
                        Id = Guid.NewGuid(),
                        Name = dto.Name?.Trim() ?? "Unnamed",
                        PhoneNumber = dto.PhoneNumber.Trim(),
                        Email = dto.Email?.Trim(),
                        Notes = dto.Notes,
                        BusinessId = businessId,
                        CreatedAt = DateTime.UtcNow
                    };

                    contactsToAdd.Add(contact);
                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new CsvImportError
                    {
                        RowNumber = row,
                        ErrorMessage = ex.Message
                    });
                }
            }

            await _db.Contacts.AddRangeAsync(contactsToAdd);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Bulk import completed: {Imported} contacts, {Errors} errors",
                result.Imported, result.Errors.Count);

            return result;
        }
        public async Task<IEnumerable<ContactDto>> GetContactsByTagsAsync(Guid businessId, List<Guid> tagIds)
        {
            // Step 1: Prepare base query (without Include yet)
            var baseQuery = _db.Contacts
                .Where(c => c.BusinessId == businessId && !c.IsArchived);

            // Step 2: Apply tag filter only if tagIds are provided
            if (tagIds?.Any() == true)
            {
                baseQuery = baseQuery.Where(c =>
                    c.ContactTags.Any(ct =>
                        tagIds.Contains(ct.TagId)
                    )
                );
            }

            // Step 3: Add Includes after filtering to avoid cast issue
            var queryWithIncludes = baseQuery
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag);

            // Step 4: Fetch data
            var contacts = await queryWithIncludes.ToListAsync();

            // Step 5: Project to DTO
            return contacts.Select(c => new ContactDto
            {
                Id = c.Id,
                Name = c.Name,
                PhoneNumber = c.PhoneNumber,
                Tags = c.ContactTags.Select(ct => new ContactTagDto
                {
                    TagId = ct.Tag.Id,
                    TagName = ct.Tag.Name,
                    ColorHex = ct.Tag.ColorHex,
                    Category = ct.Tag.Category
                }).ToList()
            });
        }
        public async Task<bool> AssignTagsAsync(Guid businessId, string phoneNumber, List<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return false;

            // 🧠 Step 1: Find the contact by phone
            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == phoneNumber && !c.IsArchived);

            if (contact == null)
                return false;

            foreach (var tagName in tags)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                // ✅ Step 2: Find or create the tag (by name)
                var tag = await _db.Tags
                    .FirstOrDefaultAsync(t => t.BusinessId == businessId && t.Name == tagName && t.IsActive);

                if (tag == null)
                {
                    tag = new Tag
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Name = tagName,
                        ColorHex = "#8c8c8c", // default gray if not assigned
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Tags.Add(tag);
                }

                // 🧪 Step 3: Check if contact already has this tag
                var alreadyTagged = await _db.ContactTags.AnyAsync(ct =>
                    ct.ContactId == contact.Id && ct.TagId == tag.Id);

                if (!alreadyTagged)
                {
                    _db.ContactTags.Add(new ContactTag
                    {
                        Id = Guid.NewGuid(),
                        ContactId = contact.Id,
                        TagId = tag.Id
                    });
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }

    }
}

