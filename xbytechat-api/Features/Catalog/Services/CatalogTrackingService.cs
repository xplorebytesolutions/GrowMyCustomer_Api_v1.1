using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Features.Catalog.Models;
using xbytechat.api.Helpers;
using xbytechat.api.Models;
using xbytechat.api.Services.Messages.Interfaces;
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Features.CRM.Timelines.Services;

namespace xbytechat.api.Features.Catalog.Services
{
    public class CatalogTrackingService : ICatalogTrackingService
    {
        private readonly AppDbContext _context;
        private readonly IMessageService _messageService;
        private readonly ILeadTimelineService _leadTimelineService;

        public CatalogTrackingService(
            AppDbContext context,
            IMessageService messageService,
            ILeadTimelineService leadTimelineService)
        {
            _context = context;
            _messageService = messageService;
            _leadTimelineService = leadTimelineService;
        }

        public async Task<ResponseResult> LogClickAsync(CatalogClickLogDto dto)
        {
            if (dto == null || dto.BusinessId == Guid.Empty || string.IsNullOrWhiteSpace(dto.UserPhone))
            {
                Log.Warning("❌ Invalid catalog click log attempt: missing businessId or userPhone.");
                return ResponseResult.ErrorInfo("Required fields are missing (businessId, userPhone).");
            }

            try
            {
                Guid? campaignSendLogId = null;
                Guid? contactId = null;
                bool followUpSent = false;

                // STEP 1: Link campaign log via RefMessageId if available
                if (!string.IsNullOrWhiteSpace(dto.RefMessageId))
                {
                    var sendLog = await _context.CampaignSendLogs
                        .FirstOrDefaultAsync(x => x.MessageId == dto.RefMessageId);

                    if (sendLog != null)
                    {
                        sendLog.IsClicked = true;
                        sendLog.ClickedAt = DateTime.UtcNow;
                        sendLog.ClickType = dto.CTAJourney ?? dto.ButtonText;
                        campaignSendLogId = sendLog.Id;
                    }
                }

                // STEP 2: Link or Create CRM Contact
                if (!string.IsNullOrWhiteSpace(dto.UserPhone))
                {
                    var contact = await _context.Contacts
                        .FirstOrDefaultAsync(c => c.PhoneNumber == dto.UserPhone && c.BusinessId == dto.BusinessId);

                    if (contact == null)
                    {
                        contact = new Contact
                        {
                            Id = Guid.NewGuid(),
                            Name = dto.UserName ?? "Lead",
                            PhoneNumber = dto.UserPhone,
                            BusinessId = dto.BusinessId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Contacts.Add(contact);
                        await _context.SaveChangesAsync();
                    }

                    contactId = contact.Id;

                    // STEP 3: Auto follow-up if plan allows
                    var business = await _context.Businesses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(b => b.Id == dto.BusinessId);

                    if (business?.BusinessPlanInfo?.Plan == PlanType.Advanced)
                    {
                        var message = $"Hi {contact.Name ?? "there"}, how can I help you?";
                        await _messageService.SendFollowUpAsync(contact.PhoneNumber, message);
                        followUpSent = true;
                    }
                }

                // STEP 4: Save the click with all linked data
                var log = new CatalogClickLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    ProductId = dto.ProductId,
                    UserId = dto.UserId,
                    UserName = dto.UserName,
                    UserPhone = dto.UserPhone,
                    BotId = dto.BotId,
                    CategoryBrowsed = dto.CategoryBrowsed,
                    ProductBrowsed = dto.ProductBrowsed,
                    CTAJourney = dto.CTAJourney,
                    TemplateId = dto.TemplateId,
                    ButtonText = dto.ButtonText,
                    RefMessageId = dto.RefMessageId,
                    ClickedAt = DateTime.UtcNow,
                    CampaignSendLogId = campaignSendLogId,
                    ContactId = contactId,
                    FollowUpSent = followUpSent,
                    LastInteractionType = "Clicked",
                    PlanSnapshot = dto.PlanSnapshot
                };

                _context.CatalogClickLogs.Add(log);
                await _context.SaveChangesAsync();

                // STEP 5: Save into LeadTimeline (no await error)
                await _leadTimelineService.AddFromCatalogClickAsync(log);

                Log.Information("📊 Catalog click tracked: {BusinessId}, {UserPhone}, {CTA}", dto.BusinessId, dto.UserPhone, dto.CTAJourney);
                return ResponseResult.SuccessInfo("✅ Click tracked successfully.", log.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to log catalog click");
                return ResponseResult.ErrorInfo("❌ Error while tracking catalog click.", ex.Message);
            }
        }

        public async Task<ResponseResult> GetRecentLogsAsync(int limit)
        {
            try
            {
                var logs = await _context.CatalogClickLogs
                    .OrderByDescending(x => x.ClickedAt)
                    .Take(limit)
                    .ToListAsync();

                return ResponseResult.SuccessInfo("Recent logs fetched.", logs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to fetch recent catalog click logs");
                return ResponseResult.ErrorInfo("Failed to fetch logs.", ex.Message);
            }
        }
    }
}
