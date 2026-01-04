using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public class CampaignRetargetService : ICampaignRetargetService
    {
        private readonly AppDbContext _db;
        private readonly ICampaignService _campaignService;

        public CampaignRetargetService(AppDbContext db, ICampaignService campaignService)
        {
            _db = db;
            _campaignService = campaignService;
        }

        public async Task<RetargetCampaignResponseDto> CreateRetargetCampaignAsync(
            Guid businessId,
            RetargetCampaignRequestDto dto,
            string createdBy,
            CancellationToken ct)
        {
            if (dto?.Campaign == null)
                throw new ArgumentException("Campaign payload is required.");

            // 1) Enforce name presence (pre-validation in controller is better, but guard here too)
            var campaignName = dto.Name?.Trim() ?? dto.Campaign.Name?.Trim();
            if (string.IsNullOrWhiteSpace(campaignName))
                throw new ArgumentException("Campaign name is required for retargeting.");

            dto.Campaign.Name = campaignName;

            // 2) Combine ContactIds and RecipientPhones
            var contactIds = dto.ContactIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new();
            var phones = dto.RecipientPhones?.Where(x => !string.IsNullOrWhiteSpace(x))
                                            .Select(x => x.Trim())
                                            .Distinct()
                                            .ToList() ?? new();

            if (phones.Any())
            {
                var resolvedIds = await _db.Contacts
                    .AsNoTracking()
                    .Where(c => c.BusinessId == businessId && phones.Contains(c.PhoneNumber))
                    .Select(c => c.Id)
                    .ToListAsync(ct);
                
                contactIds.AddRange(resolvedIds);
                contactIds = contactIds.Distinct().ToList();
            }

            // 3) Populate Campaign DTO for reuse
            dto.Campaign.ContactIds = contactIds;
            
            // 4) Call existing CampaignService logic (handles name guards, templates, jobs, etc.)
            // Status is forced to 'Draft' or 'Scheduled' inside CreateTextCampaignAsync based on dto.ScheduledAt
            var newId = await _campaignService.CreateTextCampaignAsync(dto.Campaign, businessId, createdBy);

            if (newId == null)
                throw new Exception("Failed to create campaign via CampaignService.");

            return new RetargetCampaignResponseDto
            {
                NewCampaignId = newId.Value,
                MaterializedRecipients = contactIds.Count,
                SkippedDuplicates = 0 // Deduplication is handled by CampaignService (unique ContactIds)
            };
        }
    }
}
