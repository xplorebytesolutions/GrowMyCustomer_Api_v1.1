using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Services;

namespace xbytechat.api.Features.CRM.Services
{
    public class ContactTagService : IContactTagService
    {
        private readonly AppDbContext _db;
        private readonly ITimelineService _timelineService;
        private readonly ILogger<ContactTagService> _logger;

        public ContactTagService(AppDbContext db, ITimelineService timelineService, ILogger<ContactTagService> logger)
        {
            _db = db;
            _timelineService = timelineService;
            _logger = logger;
        }

        public async Task<bool> RemoveTagFromContactAsync(Guid businessId, Guid contactId, Guid tagId)
        {
            // ✅ Find the link row but confirm contact belongs to this business
            var link = await _db.ContactTags
                .Join(_db.Contacts,
                    ct => ct.ContactId,
                    c => c.Id,
                    (ct, c) => new { ct, c })
                .Where(x => x.c.BusinessId == businessId
                            && x.c.IsActive
                            && x.ct.ContactId == contactId
                            && x.ct.TagId == tagId)
                .Select(x => x.ct)
                .FirstOrDefaultAsync();

            if (link == null) return false;

            _db.ContactTags.Remove(link);
            await _db.SaveChangesAsync();

            // ✅ Timeline log (safe, do not fail delete if timeline fails)
            try
            {
                await _timelineService.LogTagRemovedAsync(new CRMTimelineLogDto
                {
                    ContactId = contactId,
                    BusinessId = businessId,
                    EventType = "TagRemoved",
                    Description = $"🏷️ Tag removed.",
                    ReferenceId = tagId,
                    CreatedBy = "system",
                    Timestamp = DateTime.UtcNow,
                    Category = "CRM"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Timeline log failed for TagRemoved. ContactId={ContactId}, TagId={TagId}", contactId, tagId);
            }

            return true;
        }
    }
}
