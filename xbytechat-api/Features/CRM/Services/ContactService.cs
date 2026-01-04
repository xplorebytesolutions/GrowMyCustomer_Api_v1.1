// 📄 File: xbytechat-api/Features/CRM/Services/ContactService.cs

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
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

        public async Task<ResponseResult> AddContactAsync(Guid businessId, ContactDto dto)
        {
            _logger.LogInformation("📩 AddContactAsync called for businessId={BusinessId}, Name={Name}", businessId, dto.Name);

            try
            {
                var normalizedPhone = NormalizePhone(dto.PhoneNumber);

                if (string.IsNullOrWhiteSpace(normalizedPhone))
                    return ResponseResult.ErrorInfo("❌ Phone number is invalid. Please enter a valid number.");

                var existingContact = await _db.Contacts.FirstOrDefaultAsync(c =>
                    c.BusinessId == businessId && c.PhoneNumber == normalizedPhone);

                if (existingContact != null)
                {
                    _logger.LogWarning("⚠️ Duplicate contact attempt for phone {Phone}", dto.PhoneNumber);
                    return ResponseResult.ErrorInfo($"❌ A contact with the phone number '{dto.PhoneNumber}' already exists.");
                }

                var contact = new Contact
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = dto.Name,
                    PhoneNumber = normalizedPhone, // ✅ canonical digits-only
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

            var contact = await _db.Contacts
                .Where(c => c.BusinessId == businessId && c.Id == contactId && c.IsActive)
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .FirstOrDefaultAsync();

            if (contact == null)
                return null;

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

        public async Task<bool> UpdateContactAsync(Guid businessId, ContactDto dto)
        {
            _logger.LogInformation("UpdateContactAsync: businessId={BusinessId}, contactId={ContactId}", businessId, dto.Id);

            var contact = await _db.Contacts
                .Include(c => c.ContactTags)
                .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == dto.Id);

            if (contact == null)
                return false;

            contact.Name = dto.Name;

            var normalizedPhone = NormalizePhone(dto.PhoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
                throw new ArgumentException("Invalid phone number. Use E.164 digits-only (country code + number).");

            var phoneExists = await _db.Contacts.AnyAsync(c =>
                c.BusinessId == businessId &&
                c.Id != dto.Id &&
                c.PhoneNumber == normalizedPhone);

            if (phoneExists)
                throw new ArgumentException("A contact with this phone number already exists.");

            contact.PhoneNumber = normalizedPhone;
            contact.Email = dto.Email;
            contact.LeadSource = dto.LeadSource;
            contact.LastContactedAt = dto.LastContactedAt?.ToUniversalTime();
            contact.NextFollowUpAt = dto.NextFollowUpAt?.ToUniversalTime();
            contact.Notes = dto.Notes;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteContactAsync(Guid businessId, Guid contactId)
        {
            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId && c.IsActive);

            if (contact == null)
                return false;

            contact.IsActive = false;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<CsvImportResult<ContactDto>> ParseCsvToContactsAsync(Guid businessId, Stream csvStream)
        {
            _logger.LogInformation("ParseCsvToContactsAsync: businessId={BusinessId}", businessId);

            var result = new CsvImportResult<ContactDto>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null,
                PrepareHeaderForMatch = args =>
                    (args.Header ?? string.Empty)
                        .Trim()
                        .Replace(" ", "")
                        .Replace("_", "")
                        .ToLowerInvariant()
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);

            csv.Context.RegisterClassMap<ContactDtoCsvMap>();

            int rowNumber = 1;

            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                rowNumber++;
                try
                {
                    var record = csv.GetRecord<ContactDto>();

                    // ✅ Normalize phone even during parse (so user sees what will be saved)
                    var normalized = NormalizePhone(record.PhoneNumber);
                    if (string.IsNullOrWhiteSpace(normalized))
                    {
                        result.Errors.Add(new CsvImportError
                        {
                            RowNumber = rowNumber,
                            ErrorMessage = "Invalid phone number (could not normalize to E.164 digits-only)."
                        });
                        continue;
                    }

                    record.PhoneNumber = normalized;
                    record.CreatedAt = DateTime.UtcNow;

                    result.SuccessRecords.Add(record);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new CsvImportError
                    {
                        RowNumber = rowNumber,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return result;
        }

        private string NormalizePhone(string phoneNumber)
        {
            var normalized = PhoneNumberNormalizer.NormalizeToE164Digits(phoneNumber, "IN");
            return normalized ?? string.Empty; // canonical: E.164 digits-only (no '+')
        }

        public async Task<Contact> FindOrCreateAsync(Guid businessId, string phoneNumber)
        {
            var normalized = NormalizePhone(phoneNumber);

            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == normalized);

            if (contact != null)
                return contact;

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
            return newContact;
        }

        public async Task<bool> ToggleFavoriteAsync(Guid businessId, Guid contactId)
        {
            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId);
            if (contact == null) return false;

            contact.IsFavorite = !contact.IsFavorite;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task AssignTagToContactsAsync(Guid businessId, List<Guid> contactIds, Guid tagId)
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
        }

        public async Task<bool> ToggleArchiveAsync(Guid businessId, Guid contactId)
        {
            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId);
            if (contact == null) return false;

            contact.IsArchived = !contact.IsArchived;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ContactDto>> GetAllContactsAsync(Guid businessId, string? tab = "all")
        {
            var baseQuery = _db.Contacts
                .Where(c => c.BusinessId == businessId && c.IsActive);

            if (tab == "favourites")
                baseQuery = baseQuery.Where(c => c.IsFavorite);
            else if (tab == "archived")
                baseQuery = baseQuery.Where(c => c.IsArchived);
            else if (tab == "groups")
                baseQuery = baseQuery.Where(c => !string.IsNullOrEmpty(c.Group));

            var contacts = await baseQuery
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .ToListAsync();

            return contacts.Select(c => new ContactDto
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
        }

        public async Task<PagedResult<ContactDto>> GetPagedContactsAsync(Guid businessId, string? tab, int page, int pageSize, string? searchTerm)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 100) pageSize = 100;

            var baseQuery = _db.Contacts
                .Where(c => c.BusinessId == businessId && c.IsActive);

            if (string.IsNullOrWhiteSpace(tab) || tab == "all")
                baseQuery = baseQuery.Where(c => !c.IsArchived);

            if (tab == "favourites")
                baseQuery = baseQuery.Where(c => c.IsFavorite);
            else if (tab == "archived")
                baseQuery = baseQuery.Where(c => c.IsArchived);
            else if (tab == "groups")
                baseQuery = baseQuery.Where(c => !string.IsNullOrEmpty(c.Group));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                baseQuery = baseQuery.Where(c =>
                    (c.Name != null && EF.Functions.Like(c.Name, $"%{term}%")) ||
                    (c.PhoneNumber != null && EF.Functions.Like(c.PhoneNumber, $"%{term}%")) ||
                    (c.Email != null && EF.Functions.Like(c.Email, $"%{term}%"))
                );
            }

            var totalCount = await baseQuery.CountAsync();

            var contacts = await baseQuery
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .OrderBy(c => c.Name)
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
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<ContactDto>> GetContactsByTagsAsync(Guid businessId, List<Guid> tagIds)
        {
            var baseQuery = _db.Contacts
                .Where(c => c.BusinessId == businessId && !c.IsArchived);

            if (tagIds?.Any() == true)
            {
                baseQuery = baseQuery.Where(c => c.ContactTags.Any(ct => tagIds.Contains(ct.TagId)));
            }

            var contacts = await baseQuery
                .Include(c => c.ContactTags)
                    .ThenInclude(ct => ct.Tag)
                .ToListAsync();

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
            var normalizedPhone = NormalizePhone(phoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone)) return false;
            phoneNumber = normalizedPhone;

            if (tags == null || tags.Count == 0)
                return false;

            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == phoneNumber && !c.IsArchived);

            if (contact == null)
                return false;

            foreach (var tagName in tags)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                var cleanName = tagName.Trim();

                var tag = await _db.Tags
                    .FirstOrDefaultAsync(t => t.BusinessId == businessId && t.Name == cleanName && t.IsActive);

                if (tag == null)
                {
                    tag = new Tag
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Name = cleanName,
                        ColorHex = "#8c8c8c",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Tags.Add(tag);
                }

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



        public async Task<BulkImportResultDto> BulkImportAsync(Guid businessId, Stream csvStream)
        {
            _logger.LogInformation("Bulk import started for businessId={BusinessId}", businessId);

            var result = new BulkImportResultDto();

            var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null,
                PrepareHeaderForMatch = args =>
                    (args.Header ?? string.Empty)
                        .Trim()
                        .Replace(" ", "")
                        .Replace("_", "")
                        .ToLowerInvariant()
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, config);

            csv.Context.RegisterClassMap<ContactDtoCsvMap>();

            await csv.ReadAsync();
            csv.ReadHeader();

            // Collect rows first so we can query DB only for relevant phones
            var parsedRows = new List<(int Row, string Phone, string Name, string? Email, string? LeadSource, string? Notes)>();
            var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int row = 1;

            while (await csv.ReadAsync())
            {
                row++;
                try
                {
                    var dto = csv.GetRecord<ContactDto>();

                    var name = (dto.Name ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        result.Errors.Add(new CsvImportError { RowNumber = row, ErrorMessage = "Name is required." });
                        continue;
                    }

                    // Canonical normalize: digits-only E.164 (no '+')
                    var normalizedPhone = PhoneNumberNormalizer.NormalizeToE164Digits(dto.PhoneNumber, "IN");
                    if (string.IsNullOrWhiteSpace(normalizedPhone))
                    {
                        result.Errors.Add(new CsvImportError { RowNumber = row, ErrorMessage = $"Invalid phone number: '{dto.PhoneNumber}'" });
                        continue;
                    }

                    // Dedupe within the CSV
                    if (!seenInFile.Add(normalizedPhone))
                    {
                        result.DuplicatesInFile++;
                        continue;
                    }

                    var email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
                    if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
                    {
                        result.Errors.Add(new CsvImportError { RowNumber = row, ErrorMessage = $"Invalid email: '{dto.Email}'" });
                        continue;
                    }

                    parsedRows.Add((
                        Row: row,
                        Phone: normalizedPhone,
                        Name: name,
                        Email: email,
                        LeadSource: string.IsNullOrWhiteSpace(dto.LeadSource) ? null : dto.LeadSource.Trim(),
                        Notes: string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CSV parsing error at row {Row}", row);
                    result.Errors.Add(new CsvImportError { RowNumber = row, ErrorMessage = $"Parse error: {ex.Message}" });
                }
            }

            if (parsedRows.Count == 0)
            {
                _logger.LogInformation("Bulk import done. businessId={BusinessId}, nothing to import.", businessId);
                return result;
            }

            var phones = parsedRows.Select(x => x.Phone).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Fetch *all* matching contacts (including soft-deleted) for restore logic
            var existingContacts = await _db.Contacts
                .Where(c => c.BusinessId == businessId && phones.Contains(c.PhoneNumber))
                .ToListAsync();

            var existingByPhone = existingContacts
                .GroupBy(c => c.PhoneNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreatedAt).First(),
                    StringComparer.OrdinalIgnoreCase
                );

            var toInsert = new List<Contact>();

            foreach (var r in parsedRows)
            {
                if (existingByPhone.TryGetValue(r.Phone, out var existing))
                {
                    // Policy:
                    // - Active -> skip
                    // - Soft-deleted (IsActive=false) -> restore
                    // - Archived -> skip (treat as existing)
                    if (existing.IsArchived)
                    {
                        result.SkippedExisting++;
                        continue;
                    }

                    if (existing.IsActive)
                    {
                        result.SkippedExisting++;
                        continue;
                    }

                    // ✅ Restore soft-deleted
                    existing.IsActive = true;
                    existing.IsArchived = false;

                    // Update fields (safe updates: only overwrite when CSV provides data)
                    existing.Name = r.Name;
                    if (!string.IsNullOrWhiteSpace(r.Email)) existing.Email = r.Email;
                    if (!string.IsNullOrWhiteSpace(r.LeadSource)) existing.LeadSource = r.LeadSource;
                    if (!string.IsNullOrWhiteSpace(r.Notes)) existing.Notes = r.Notes;

                    result.Restored++;
                    continue;
                }

                // Brand-new insert
                toInsert.Add(new Contact
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = r.Name,
                    PhoneNumber = r.Phone,
                    Email = r.Email,
                    LeadSource = r.LeadSource,
                    Notes = r.Notes,
                    IsActive = true,
                    IsArchived = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (toInsert.Count > 0)
            {
                _db.Contacts.AddRange(toInsert);
            }

            await _db.SaveChangesAsync();

            result.Imported = toInsert.Count;

            _logger.LogInformation(
                "Bulk import done. businessId={BusinessId}, imported={Imported}, restored={Restored}, skippedExisting={SkippedExisting}, dupInFile={DupInFile}, errors={Errors}",
                businessId, result.Imported, result.Restored, result.SkippedExisting, result.DuplicatesInFile, result.Errors.Count
            );

            return result;

            static bool IsValidEmail(string email)
            {
                // EmailAddressAttribute is good enough for MVP validation
                return new EmailAddressAttribute().IsValid(email);
            }
        }


    }
}


