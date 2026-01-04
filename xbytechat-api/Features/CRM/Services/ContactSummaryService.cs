// 📄 xbytechat-api/Features/CRM/Summary/Services/ContactSummaryService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Mappers;
using xbytechat.api.Features.CRM.Timelines.Services;

namespace xbytechat.api.Features.CRM.Services
{
    /// <summary>
    /// Default implementation of IContactSummaryService.
    /// Orchestrates calls into existing CRM services and returns
    /// a single response model tailored for UI consumption.
    /// </summary>
    public sealed class ContactSummaryService : IContactSummaryService
    {
        private readonly IContactService _contactService;
        private readonly INoteService _noteService;
        private readonly IReminderService _reminderService;
        private readonly ILeadTimelineService _leadTimelineService;

        public ContactSummaryService(
            IContactService contactService,
            INoteService noteService,
            IReminderService reminderService,
            ILeadTimelineService leadTimelineService)
        {
            _contactService = contactService ?? throw new ArgumentNullException(nameof(contactService));
            _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
            _reminderService = reminderService ?? throw new ArgumentNullException(nameof(reminderService));
            _leadTimelineService = leadTimelineService ?? throw new ArgumentNullException(nameof(leadTimelineService));
        }

        public async Task<ContactSummaryResponseDto?> GetContactSummaryAsync(
            Guid businessId,
            Guid contactId,
            CancellationToken ct = default)
        {
            // 1) Core contact (this already returns ContactDto with tags)
            var contact = await _contactService.GetContactByIdAsync(businessId, contactId);
            if (contact == null)
            {
                return null;
            }

            // 2) Notes – latest 3 by CreatedAt
            var notes = await _noteService.GetNotesByContactAsync(businessId, contactId);
            var recentNotes = notes
                .OrderByDescending(n => n.CreatedAt)
                .Take(3)
                .ToList();

            // 3) Next upcoming reminder for this contact (in-memory filter from service)
            var allReminders = await _reminderService.GetAllRemindersAsync(businessId);
            var nowUtc = DateTime.UtcNow;

            var nextReminder = allReminders
                .Where(r =>
                    r.ContactId == contactId &&
                    r.IsActive &&
                    string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                    r.DueAt >= nowUtc)
                .OrderBy(r => r.DueAt)
                .FirstOrDefault();

            // 4) Recent timeline entries (latest 5 by CreatedAt)
            var timelineEntities = await _leadTimelineService.GetTimelineByContactIdAsync(contactId);

            var recentTimeline = timelineEntities
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .Select(LeadTimelineMapper.ToDto)
                .Where(dto => dto != null)
                .ToList()!; // mapper may return null, we filter just in case

            // 5) Assemble response
            return new ContactSummaryResponseDto
            {
                BusinessId = businessId,
                ContactId = contactId,

                Name = contact.Name,
                PhoneNumber = contact.PhoneNumber,
                Email = contact.Email,
                LeadSource = contact.LeadSource,
                LastContactedAt = contact.LastContactedAt,
                NextFollowUpAt = contact.NextFollowUpAt,
                IsFavorite = contact.IsFavorite,
                IsArchived = contact.IsArchived,
                Group = contact.Group,

                Tags = contact.Tags ?? new List<ContactTagDto>(),

                RecentNotes = recentNotes,
                NextReminder = nextReminder,
                RecentTimeline = recentTimeline
            };
        }
    }
}
