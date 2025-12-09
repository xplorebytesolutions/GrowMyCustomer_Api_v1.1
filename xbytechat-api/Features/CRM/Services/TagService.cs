using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Timelines.Services;
using xbytechat.api.Features.CRM.Timelines.DTOs;

namespace xbytechat.api.Features.CRM.Services
{
    public class TagService : ITagService
    {
        private readonly AppDbContext _db;
        private readonly ITimelineService _timelineService; // ✅ Injected TimelineService
        private readonly ILogger<TagService> _logger;
        public TagService(AppDbContext db, ITimelineService timelineService, ILogger<TagService> logger)
        {
            _db = db;
            _timelineService = timelineService;
            _logger = logger;
        }

        public async Task<TagDto> AddTagAsync(Guid businessId, TagDto dto)
        {
            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = dto.Name,
                ColorHex = dto.ColorHex,
                Category = dto.Category,
                Notes = dto.Notes,
                IsSystemTag = dto.IsSystemTag,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = null
            };

            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // ✅ After saving tag → try logging into Timeline (non-blocking)
            try
            {
                await _timelineService.LogTagAppliedAsync(new CRMTimelineLogDto
                {
                    ContactId = Guid.Empty,    // ➡️ No specific contact, general event
                    BusinessId = businessId,
                    EventType = "TagCreated",
                    Description = $"🏷️ New tag created: {dto.Name}",
                    ReferenceId = tag.Id,
                    CreatedBy = "System",
                    Timestamp = DateTime.UtcNow,
                    Category = "CRM"
                });
            }
            catch (Exception ex)
            {
                // 🛡 Fail-safe: Do not block tag creation if timeline fails
                Console.WriteLine($"⚠️ Timeline log failed for TagId {tag.Id}: {ex.Message}");
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

            tag.Name = dto.Name;
            tag.ColorHex = dto.ColorHex;
            tag.Category = dto.Category;
            tag.Notes = dto.Notes;
            tag.IsSystemTag = dto.IsSystemTag;
            tag.IsActive = dto.IsActive;
            tag.LastUsedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTagAsync(Guid businessId, Guid tagId)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.BusinessId == businessId);
            if (tag == null) return false;

            tag.IsActive = false;
            await _db.SaveChangesAsync();
            return true;
        }
        //public async Task AssignTagAsync(Guid businessId, string phone, string tag)
        //{
        //    try
        //    {
        //        // ✅ Step 1: Lookup contact
        //        var contact = await _db.Contacts
        //            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == phone);

        //        if (contact == null)
        //        {
        //            _logger.LogWarning("⚠️ Contact not found for phone: {Phone}", phone);
        //            return;
        //        }

        //        // ✅ Step 2: Check if tag exists
        //        var existingTag = await _db.Tags
        //            .FirstOrDefaultAsync(t => t.BusinessId == businessId && t.Name == tag);

        //        if (existingTag == null)
        //        {
        //            existingTag = new Tag
        //            {
        //                Id = Guid.NewGuid(),
        //                BusinessId = businessId,
        //                Name = tag,
        //                CreatedAt = DateTime.UtcNow
        //            };

        //            await _db.Tags.AddAsync(existingTag);
        //        }

        //        // ✅ Step 3: Associate tag with contact if not already
        //        var alreadyTagged = await _db.ContactTags
        //            .AnyAsync(ct => ct.ContactId == contact.Id && ct.TagId == existingTag.Id);

        //        if (!alreadyTagged)
        //        {
        //            await _db.ContactTags.AddAsync(new ContactTag
        //            {
        //                Id = Guid.NewGuid(),
        //                ContactId = contact.Id,
        //                TagId = existingTag.Id,
        //                AssignedAt = DateTime.UtcNow
        //            });

        //            _logger.LogInformation("🏷 Tag '{Tag}' assigned to contact {ContactId}", tag, contact.Id);
        //        }

        //        await _db.SaveChangesAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "❌ Error assigning tag to contact.");
        //        throw;
        //    }
        //}
        public async Task AssignTagsAsync(Guid businessId, string phoneNumber, List<string> tagNames)
        {
            if (tagNames == null || !tagNames.Any())
                return;

            // 🔍 Fetch the contact and existing tag links
            var contact = await _db.Contacts
             .Include(c => c.ContactTags)
             .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == phoneNumber);


            if (contact == null) return;

            var existingTagIds = contact.ContactTags.Select(t => t.TagId).ToHashSet();

            // 🔍 Ensure tags exist or create them
            var tags = await _db.Tags
                .Where(t => t.BusinessId == businessId && tagNames.Contains(t.Name))
                .ToListAsync();

            var existingNames = tags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingNames = tagNames.Where(t => !existingNames.Contains(t)).Distinct().ToList();

            foreach (var name in missingNames)
            {
                var newTag = new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    BusinessId = businessId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Tags.Add(newTag);
                tags.Add(newTag);
            }

            await _db.SaveChangesAsync(); // Save new tags before linking

            // ✅ Link new tags to contact
            foreach (var tag in tags)
            {
                if (!existingTagIds.Contains(tag.Id))
                {
                    contact.ContactTags.Add(new ContactTag
                    {
                        Id = Guid.NewGuid(),
                        TagId = tag.Id,
                        ContactId = contact.Id,
                        BusinessId = businessId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = "automation" // optional: set to flow name
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
