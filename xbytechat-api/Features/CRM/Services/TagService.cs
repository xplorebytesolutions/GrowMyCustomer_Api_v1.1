// 📄 File: xbytechat-api/Features/CRM/Services/TagService.cs

using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Services;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CRM.Services
{
    public class TagService : ITagService
    {
        private readonly AppDbContext _db;
        private readonly ITimelineService _timelineService;
        private readonly ILogger<TagService> _logger;

        public TagService(AppDbContext db, ITimelineService timelineService, ILogger<TagService> logger)
        {
            _db = db;
            _timelineService = timelineService;
            _logger = logger;
        }

        public async Task<TagDto> AddTagAsync(Guid businessId, TagDto dto)
        {
            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tag name is required.", nameof(dto.Name));

            // ✅ Avoid duplicates (case-insensitive) within a business
            // NOTE: This is app-level protection; a DB unique index is still recommended later.
            var nameLower = name.ToLowerInvariant();

            var existing = await _db.Tags
                .Where(t => t.BusinessId == businessId && t.IsActive)
                .FirstOrDefaultAsync(t => (t.Name ?? "").ToLower() == nameLower);

            if (existing != null)
            {
                return new TagDto
                {
                    Id = existing.Id,
                    Name = existing.Name,
                    ColorHex = existing.ColorHex,
                    Category = existing.Category,
                    Notes = existing.Notes,
                    IsSystemTag = existing.IsSystemTag,
                    IsActive = existing.IsActive,
                    CreatedAt = existing.CreatedAt,
                    LastUsedAt = existing.LastUsedAt
                };
            }

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = name,
                ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#8c8c8c" : dto.ColorHex.Trim(),
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                IsSystemTag = dto.IsSystemTag,

                // ✅ IMPORTANT: do not trust dto.IsActive during create (missing bool => false)
                IsActive = true,

                CreatedAt = DateTime.UtcNow,
                LastUsedAt = null
            };

            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // ✅ Non-blocking timeline log
            try
            {
                await _timelineService.LogTagAppliedAsync(new CRMTimelineLogDto
                {
                    ContactId = Guid.Empty,
                    BusinessId = businessId,
                    EventType = "TagCreated",
                    Description = $"🏷️ New tag created: {tag.Name}",
                    ReferenceId = tag.Id,
                    CreatedBy = "System",
                    Timestamp = DateTime.UtcNow,
                    Category = "CRM"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Timeline log failed for TagId {TagId}", tag.Id);
            }

            return new TagDto
            {
                Id = tag.Id,
                Name = tag.Name,
                ColorHex = tag.ColorHex,
                Category = tag.Category,
                Notes = tag.Notes,
                IsSystemTag = tag.IsSystemTag,
                IsActive = tag.IsActive,
                CreatedAt = tag.CreatedAt,
                LastUsedAt = tag.LastUsedAt
            };
        }

        public async Task<IEnumerable<TagDto>> GetAllTagsAsync(Guid businessId)
        {
            return await _db.Tags
                .Where(t => t.BusinessId == businessId && t.IsActive)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TagDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    ColorHex = t.ColorHex,
                    Category = t.Category,
                    Notes = t.Notes,
                    IsSystemTag = t.IsSystemTag,
                    IsActive = t.IsActive,
                    CreatedAt = t.CreatedAt,
                    LastUsedAt = t.LastUsedAt
                })
                .ToListAsync();
        }

        public async Task<bool> UpdateTagAsync(Guid businessId, Guid tagId, TagDto dto)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.BusinessId == businessId);
            if (tag == null) return false;

            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tag name is required.", nameof(dto.Name));

            // ✅ Prevent rename collision (case-insensitive) inside business
            var nameLower = name.ToLowerInvariant();

            var collision = await _db.Tags
                .Where(t => t.BusinessId == businessId && t.IsActive && t.Id != tagId)
                .AnyAsync(t => (t.Name ?? "").ToLower() == nameLower);

            if (collision)
                throw new InvalidOperationException($"A tag with name '{name}' already exists.");

            tag.Name = name;
            tag.ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? tag.ColorHex : dto.ColorHex.Trim();
            tag.Category = string.IsNullOrWhiteSpace(dto.Category) ? tag.Category : dto.Category.Trim();
            tag.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            tag.IsSystemTag = dto.IsSystemTag;

            // ✅ Allow activate/deactivate via update
            tag.IsActive = dto.IsActive;
            tag.LastUsedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTagAsync(Guid businessId, Guid tagId)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.BusinessId == businessId);
            if (tag == null) return false;

            tag.IsActive = false; // ✅ soft delete
            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Assigns tags (by name) to a contact located via phone number.
        /// Canonical phone storage: E.164 digits-only (no '+').
        /// </summary>
        public async Task<bool> AssignTagsAsync(Guid businessId, string phoneNumber, List<string> tagNames)
        {
            // ✅ Normalize phone first (digits-only)
            var normalizedPhone = PhoneNumberNormalizer.NormalizeToE164Digits(phoneNumber, "IN");
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                _logger.LogWarning("AssignTagsAsync: invalid phone. businessId={BusinessId}, rawPhone={Phone}", businessId, phoneNumber);
                return false;
            }

            if (tagNames == null || tagNames.Count == 0)
                return false;

            // ✅ Clean tag list (trim + remove empties + distinct, case-insensitive)
            var cleanedTagNames = tagNames
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanedTagNames.Count == 0)
                return false;

            // ✅ Find contact safely (tenant + active + not archived)
            var contact = await _db.Contacts
                .Include(c => c.ContactTags)
                .FirstOrDefaultAsync(c =>
                    c.BusinessId == businessId &&
                    c.IsActive &&
                    !c.IsArchived &&
                    c.PhoneNumber == normalizedPhone);

            if (contact == null)
            {
                _logger.LogWarning("AssignTagsAsync: contact not found. businessId={BusinessId}, phone={Phone}", businessId, normalizedPhone);
                return false;
            }

            var existingTagIds = contact.ContactTags?.Select(ct => ct.TagId).ToHashSet() ?? new HashSet<Guid>();

            // ✅ Fetch existing tags (active). Case-insensitive mapping in-memory (safe across DB collations).
            var existingTags = await _db.Tags
                .Where(t => t.BusinessId == businessId && t.IsActive)
                .ToListAsync();

            var existingByName = existingTags
                .GroupBy(t => t.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var tagsToLink = new List<Tag>();

            foreach (var name in cleanedTagNames)
            {
                if (existingByName.TryGetValue(name, out var tag))
                {
                    tagsToLink.Add(tag);
                    continue;
                }

                // Create missing tag
                var newTag = new Tag
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = name,
                    ColorHex = "#8c8c8c",
                    Category = "General",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };

                _db.Tags.Add(newTag);
                tagsToLink.Add(newTag);
                existingByName[name] = newTag;
            }

            // Save new tags before linking
            await _db.SaveChangesAsync();

            contact.ContactTags ??= new List<ContactTag>();

            var anyLinked = false;

            foreach (var tag in tagsToLink)
            {
                if (existingTagIds.Contains(tag.Id))
                    continue;

                contact.ContactTags.Add(new ContactTag
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    ContactId = contact.Id,
                    TagId = tag.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = "automation"
                });

                anyLinked = true;
            }

            if (!anyLinked)
                return true;

            await _db.SaveChangesAsync();

            // ✅ Non-blocking timeline
            try
            {
                await _timelineService.LogTagAppliedAsync(new CRMTimelineLogDto
                {
                    ContactId = contact.Id,
                    BusinessId = businessId,
                    EventType = "TagsAssigned",
                    Description = $"🏷️ Tags assigned: {string.Join(", ", cleanedTagNames)}",
                    ReferenceId = contact.Id,
                    CreatedBy = "automation",
                    Timestamp = DateTime.UtcNow,
                    Category = "CRM"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Timeline log failed for AssignTagsAsync. contactId={ContactId}", contact.Id);
            }

            return true;
        }
    }
}
