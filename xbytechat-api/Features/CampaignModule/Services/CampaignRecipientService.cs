using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public class CampaignRecipientService : ICampaignRecipientService
    {
        private readonly AppDbContext _context;

        public CampaignRecipientService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CampaignRecipientDto?> GetByIdAsync(Guid businessId, Guid id)
        {
            return await _context.CampaignRecipients
                .AsNoTracking()
                .Include(r => r.Contact)
                .Include(r => r.AudienceMember)
                .Where(r => r.BusinessId == businessId && r.Id == id)
                .Select(r => new CampaignRecipientDto
                {
                    Id = r.Id,
                    ContactId = r.ContactId,
                    ContactName = r.Contact != null
                        ? (r.Contact.Name ?? string.Empty)
                        : (r.AudienceMember != null ? (r.AudienceMember.Name ?? string.Empty) : string.Empty),
                    ContactPhone = r.Contact != null
                        ? (r.Contact.PhoneNumber ?? string.Empty)
                        : (r.AudienceMember != null ? (r.AudienceMember.PhoneE164 ?? r.AudienceMember.PhoneRaw ?? string.Empty) : string.Empty),
                    Status = r.Status,
                    SentAt = r.SentAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<CampaignRecipientDto>> GetByCampaignIdAsync(Guid businessId, Guid campaignId)
        {
            return await _context.CampaignRecipients
                .AsNoTracking()
                .Include(r => r.Contact)
                .Include(r => r.AudienceMember)
                .Where(r => r.BusinessId == businessId && r.CampaignId == campaignId)
                .Select(r => new CampaignRecipientDto
                {
                    Id = r.Id,
                    ContactId = r.ContactId,
                    ContactName = r.Contact != null
                        ? (r.Contact.Name ?? string.Empty)
                        : (r.AudienceMember != null ? (r.AudienceMember.Name ?? string.Empty) : string.Empty),
                    ContactPhone = r.Contact != null
                        ? (r.Contact.PhoneNumber ?? string.Empty)
                        : (r.AudienceMember != null ? (r.AudienceMember.PhoneE164 ?? r.AudienceMember.PhoneRaw ?? string.Empty) : string.Empty),
                    Status = r.Status,
                    SentAt = r.SentAt
                })
                .ToListAsync();
        }

        public async Task<bool> UpdateStatusAsync(Guid businessId, Guid recipientId, string newStatus)
        {
            var recipient = await _context.CampaignRecipients
                .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == recipientId);
            if (recipient == null) return false;

            recipient.Status = newStatus;
            recipient.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> TrackReplyAsync(Guid businessId, Guid recipientId, string replyText)
        {
            var recipient = await _context.CampaignRecipients
                .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Id == recipientId);
            if (recipient == null) return false;

            recipient.ClickedCTA = replyText;
            recipient.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<CampaignRecipientDto>> SearchRecipientsAsync(Guid businessId, string? status = null, string? keyword = null)
        {
            var query = _context.CampaignRecipients
                .AsNoTracking()
                .Include(r => r.Contact)
                .Include(r => r.AudienceMember)
                .Where(r => r.BusinessId == businessId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(r =>
                    (r.Contact != null &&
                     ((r.Contact.Name != null && r.Contact.Name.Contains(keyword)) ||
                      (r.Contact.PhoneNumber != null && r.Contact.PhoneNumber.Contains(keyword))))
                    ||
                    (r.AudienceMember != null &&
                     ((r.AudienceMember.Name != null && r.AudienceMember.Name.Contains(keyword)) ||
                      (r.AudienceMember.PhoneE164 != null && r.AudienceMember.PhoneE164.Contains(keyword)) ||
                      (r.AudienceMember.PhoneRaw != null && r.AudienceMember.PhoneRaw.Contains(keyword)))));
            }

            return await query
                .Select(r => new CampaignRecipientDto
                {
                    Id = r.Id,
                    ContactId = r.ContactId,
                    ContactName = r.Contact != null
                        ? (r.Contact.Name ?? string.Empty)
                        : (r.AudienceMember != null ? (r.AudienceMember.Name ?? string.Empty) : string.Empty),
                    ContactPhone = r.Contact != null
                        ? (r.Contact.PhoneNumber ?? string.Empty)
                        : (r.AudienceMember != null ? (r.AudienceMember.PhoneE164 ?? r.AudienceMember.PhoneRaw ?? string.Empty) : string.Empty),
                    Status = r.Status,
                    SentAt = r.SentAt
                })
                .ToListAsync();
        }

        public async Task AssignContactsToCampaignAsync(Guid businessId, Guid campaignId, List<Guid> contactIds)
        {
            var campaign = await _context.Campaigns
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId);

            if (campaign == null)
                throw new Exception("Campaign not found.");

            var now = DateTime.UtcNow;

            var contactIdsClean = (contactIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (contactIdsClean.Count == 0)
                return;

            var validContactIds = await _context.Contacts
                .Where(c => c.BusinessId == businessId && contactIdsClean.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            if (validContactIds.Count == 0)
                return;

            var existingContactIds = await _context.CampaignRecipients
                .Where(r => r.BusinessId == businessId
                            && r.CampaignId == campaignId
                            && r.ContactId.HasValue
                            && validContactIds.Contains(r.ContactId.Value))
                .Select(r => r.ContactId!.Value)
                .ToListAsync();

            var newContactIds = validContactIds.Except(existingContactIds).ToList();
            if (newContactIds.Count == 0)
                return;

            var newRecipients = newContactIds.Select(contactId => new CampaignRecipient
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                BusinessId = businessId,
                ContactId = contactId,
                Status = "Pending",
                MaterializedAt = now,
                SentAt = null,
                UpdatedAt = now,
                IsAutoTagged = false
            }).ToList();

            await _context.CampaignRecipients.AddRangeAsync(newRecipients);
            await _context.SaveChangesAsync();
        }
    }
}
