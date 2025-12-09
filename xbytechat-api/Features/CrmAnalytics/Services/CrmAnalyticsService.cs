using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CrmAnalytics.DTOs;
using xbytechat.api.Features.CrmAnalytics.Services;

namespace xbytechat.api.Features.CrmAnalytics.Services
{
    /// <summary>
    /// Provides implementation for CRM analytics calculations.
    /// Gathers contact, tag, note, and reminder metrics for the dashboard.
    /// </summary>
    public class CrmAnalyticsService : ICrmAnalyticsService
    {
        private readonly AppDbContext _context;

        public CrmAnalyticsService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Generates a summarized snapshot of CRM data for the given business.
        /// </summary>
        /// <param name="businessId">The unique ID of the business</param>
        /// <returns>CrmAnalyticsSummaryDto containing insights</returns>
        public async Task<CrmAnalyticsSummaryDto> GetSummaryAsync(Guid businessId)
        {
            var today = DateTime.UtcNow.Date;

            var totalContacts = await _context.Contacts
                .CountAsync(c => c.BusinessId == businessId);

            var taggedContacts = await _context.Contacts
                .Where(c => c.BusinessId == businessId && c.Tags.Any())
                .CountAsync();

            var activeReminders = await _context.Reminders
                .CountAsync(r => r.BusinessId == businessId && r.Status == "Pending");

            var completedReminders = await _context.Reminders
                .CountAsync(r => r.BusinessId == businessId && r.Status == "Completed");

            var totalNotes = await _context.Notes
                .CountAsync(n => n.BusinessId == businessId);

            var leadsWithTimeline = await _context.LeadTimelines
                .Where(t => t.BusinessId == businessId)
                .Select(t => t.ContactId)
                .Distinct()
                .CountAsync();

            var newContactsToday = await _context.Contacts
                .CountAsync(c => c.BusinessId == businessId && c.CreatedAt.Date == today);

            var notesAddedToday = await _context.Notes
                .CountAsync(n => n.BusinessId == businessId && n.CreatedAt.Date == today);

            var lastContactAddedAt = await _context.Contacts
                .Where(c => c.BusinessId == businessId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => (DateTime?)c.CreatedAt)
                .FirstOrDefaultAsync();

            var lastReminderCompletedAt = await _context.Reminders
                .Where(r => r.BusinessId == businessId && r.Status == "Completed")
                .OrderByDescending(r => r.CompletedAt)
                .Select(r => (DateTime?)r.CompletedAt)
                .FirstOrDefaultAsync();

            return new CrmAnalyticsSummaryDto
            {
                TotalContacts = totalContacts,
                TaggedContacts = taggedContacts,
                ActiveReminders = activeReminders,
                CompletedReminders = completedReminders,
                TotalNotes = totalNotes,
                LeadsWithTimeline = leadsWithTimeline,
                NewContactsToday = newContactsToday,
                NotesAddedToday = notesAddedToday,
                LastContactAddedAt = lastContactAddedAt,
                LastReminderCompletedAt = lastReminderCompletedAt
            };
        }
        public async Task<List<ContactTrendsDto>> GetContactTrendsAsync(Guid businessId)
        {
            var trends = await _context.Contacts
                .Where(c => c.BusinessId == businessId)
                .GroupBy(c => c.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ContactTrendsDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Count = g.Count()
                })
                .ToListAsync();

            return trends;
        }

    }
}
