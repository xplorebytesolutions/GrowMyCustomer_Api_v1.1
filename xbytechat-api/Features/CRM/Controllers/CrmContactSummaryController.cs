using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CRM.DTOs;

namespace xbytechat.api.Features.CRM.Controllers
{
    /// <summary>
    /// Read-only CRM aggregation endpoint used by Chat Inbox right panel.
    /// </summary>
    [ApiController]
    [Route("api/crm")]
    public sealed class CrmContactSummaryController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<CrmContactSummaryController> _log;

        public CrmContactSummaryController(
            AppDbContext db,
            ILogger<CrmContactSummaryController> log)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Returns a compact "Contact 360" summary for Inbox.
        /// </summary>
        [HttpGet("contact-summary/{contactId:guid}")]
        public async Task<ActionResult<CrmContactSummaryDto>> GetContactSummary(
            Guid contactId,
            CancellationToken cancellationToken)
        {
            // 1️⃣ Load the contact (also gives us BusinessId for scoping)
            var contact = await _db.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == contactId, cancellationToken);

            if (contact == null)
            {
                return NotFound(new { message = "Contact not found." });
            }

            var businessId = contact.BusinessId;

            var dto = new CrmContactSummaryDto
            {
                ContactId = contact.Id,
                BusinessId = businessId,
                // Map with typical Contact fields (adjust if your entity differs)
                Name = contact.Name,
                PhoneNumber = contact.PhoneNumber,
                Email = contact.Email,
                LeadSource = contact.LeadSource,
                CreatedAt = contact.CreatedAt
            };

            // 2️⃣ Tags – simple many-to-many via ContactTags
            var tagQuery =
                from ct in _db.ContactTags.AsNoTracking()
                join t in _db.Tags.AsNoTracking()
                    on ct.TagId equals t.Id
                where ct.ContactId == contactId
                      && ct.BusinessId == businessId
                select new CrmTagChipDto
                {
                    Id = t.Id,
                    TagName = t.Name,
                    ColorHex = t.ColorHex
                };

            dto.Tags = await tagQuery
                .OrderBy(t => t.TagName)
                .ToListAsync(cancellationToken);

            // 3️⃣ Recent notes – last 3
            var noteQuery = _db.Notes
                .AsNoTracking()
                .Where(n => n.ContactId == contactId && n.BusinessId == businessId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(3)
                .Select(n => new CrmNoteSnippetDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    CreatedByName = n.CreatedBy,
                    CreatedAt = n.CreatedAt
                });

            dto.RecentNotes = await noteQuery.ToListAsync(cancellationToken);

            // 4️⃣ Next reminder – nearest future reminder (not completed)
            var nowUtc = DateTime.UtcNow;

            var nextReminderEntity = await _db.Reminders
                .AsNoTracking()
                .Where(r =>
                    r.ContactId == contactId &&
                    r.BusinessId == businessId &&
                    r.DueAt >= nowUtc &&
                    r.Status != "Completed") // using Status instead of IsCompleted
                .OrderBy(r => r.DueAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextReminderEntity != null)
            {
                dto.NextReminder = new CrmReminderSnippetDto
                {
                    Id = nextReminderEntity.Id,
                    Title = nextReminderEntity.Title,
                    Description = nextReminderEntity.Description,
                    Status = nextReminderEntity.Status,
                    DueAt = nextReminderEntity.DueAt,
                    Priority = nextReminderEntity.Priority
                };
            }

            // 5️⃣ Recent timeline – last 5 timeline events
            var timelineQuery = _db.LeadTimelines
                .AsNoTracking()
                .Where(e => e.ContactId == contactId && e.BusinessId == businessId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .Select(e => new CrmTimelineEventDto
                {
                    Id = e.Id,
                    Title = e.Description, // no Title/ShortDescription on model
                    Source = e.Source,
                    Category = e.Category,
                    EventType = e.EventType,
                    CreatedAt = e.CreatedAt
                });

            dto.RecentTimeline = await timelineQuery.ToListAsync(cancellationToken);

            _log.LogInformation(
                "[CRM] Contact summary loaded: Business={BusinessId} Contact={ContactId}",
                businessId,
                contactId);

            return Ok(dto);
        }
    }
}
