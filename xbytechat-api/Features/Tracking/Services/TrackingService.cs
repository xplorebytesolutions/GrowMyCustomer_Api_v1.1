using System;
using System.Threading.Tasks;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Features.Tracking.Models;
using xbytechat.api.Shared.TrackingUtils;
using Serilog;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CampaignModule.DTOs;
using AutoMapper;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Helpers;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CRM.Dtos;

namespace xbytechat.api.Features.Tracking.Services
{
    public class TrackingService : ITrackingService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        public TrackingService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }


        public async Task<ResponseResult> LogCTAClickWithEnrichmentAsync(TrackingLogDto dto)
        {
            try
            {
                // 🧠 1. Try enrich from MessageLog (if msg ID provided)
                if (!string.IsNullOrEmpty(dto.MessageId))
                {
                    var messageLog = await _context.MessageLogs
                        .FirstOrDefaultAsync(m => m.MessageId == dto.MessageId || m.Id.ToString() == dto.MessageId);

                    if (messageLog != null)
                    {
                        dto.BusinessId = dto.BusinessId == Guid.Empty ? messageLog.BusinessId : dto.BusinessId;
                        dto.ContactId ??= messageLog.ContactId;
                        dto.CampaignId ??= messageLog.CampaignId;
                        dto.MessageLogId ??= messageLog.Id;
                    }
                }

                // 🧩 2. Enrich from CampaignSendLog if sourceType = campaign
                CampaignSendLog? sendLog = null;

                if (dto.SourceType == "campaign")
                {
                    if (!string.IsNullOrEmpty(dto.MessageId))
                    {
                        sendLog = await _context.CampaignSendLogs
                            .Include(c => c.Recipient)
                            .FirstOrDefaultAsync(c => c.MessageId == dto.MessageId);
                    }

                    if (sendLog == null && dto.CampaignId != null)
                    {
                        sendLog = await _context.CampaignSendLogs
                            .Include(c => c.Recipient)
                            .Where(c => c.CampaignId == dto.CampaignId)
                            .OrderByDescending(c => c.CreatedAt)
                            .FirstOrDefaultAsync();
                    }

                    if (sendLog != null)
                    {
                        dto.BusinessId = dto.BusinessId == Guid.Empty
                            ? sendLog.Recipient?.BusinessId ?? Guid.Empty
                            : dto.BusinessId;

                        dto.ContactId ??= sendLog.ContactId;
                        dto.CampaignId ??= sendLog.CampaignId;
                        dto.CampaignSendLogId ??= sendLog.Id;

                        // ✅ Update campaign send log click details
                        sendLog.IsClicked = true;
                        sendLog.ClickedAt = dto.ClickedAt ?? DateTime.UtcNow;
                        sendLog.ClickType = dto.ButtonText;

                        await _context.SaveChangesAsync();
                    }
                }

                // ✅ 3. Validate business context
                if (dto.BusinessId == Guid.Empty)
                {
                    Log.Warning("❌ TrackingLog failed: No BusinessId available for click.");
                    return ResponseResult.ErrorInfo("Business context is missing.");
                }

                // 💾 4. Save tracking log (inline instead of separate method)
                var trackingLog = new TrackingLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    ContactId = dto.ContactId,
                    ContactPhone = dto.ContactPhone,
                    SourceType = dto.SourceType,
                    SourceId = dto.SourceId,
                    ButtonText = dto.ButtonText,
                    CTAType = dto.CTAType,
                    MessageId = dto.MessageId,
                    TemplateId = dto.TemplateId,
                    MessageLogId = dto.MessageLogId,
                    ClickedVia = dto.ClickedVia ?? "web",
                    Referrer = dto.Referrer,
                    ClickedAt = dto.ClickedAt ?? DateTime.UtcNow,
                    IPAddress = dto.IPAddress,
                    DeviceType = dto.DeviceType,
                    Browser = dto.Browser,
                    Country = dto.Country,
                    City = dto.City,
                    FollowUpSent = false,
                    LastInteractionType = "Clicked",
                    SessionId = Guid.TryParse(dto.SessionId, out var sid) ? sid : (Guid?)null,
                    ThreadId = Guid.TryParse(dto.ThreadId, out var tid) ? tid : (Guid?)null,
                    CampaignId = dto.CampaignId,
                    CampaignSendLogId = dto.CampaignSendLogId
                };

                await _context.TrackingLogs.AddAsync(trackingLog);
                await _context.SaveChangesAsync();

                // ✅ Return the TrackingLog.Id in the response
                return ResponseResult.SuccessInfo("CTA click tracked successfully.", trackingLog.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Exception during CTA tracking enrichment");
                return ResponseResult.ErrorInfo("Exception during tracking: " + ex.Message);
            }
        }

        public async Task LogCTAClickAsync(TrackingLogDto dto)
        {
            try
            {
                var log = new TrackingLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    ContactId = dto.ContactId,
                    ContactPhone = dto.ContactPhone,
                    SourceType = dto.SourceType,
                    SourceId = dto.SourceId,
                    ButtonText = dto.ButtonText,
                    CTAType = dto.CTAType,
                    MessageId = dto.MessageId,
                    TemplateId = dto.TemplateId,
                    MessageLogId = dto.MessageLogId,
                    ClickedVia = dto.ClickedVia ?? "web",
                    Referrer = dto.Referrer,
                    ClickedAt = dto.ClickedAt ?? DateTime.UtcNow,
                    IPAddress = dto.IPAddress,
                    DeviceType = dto.DeviceType,
                    Browser = dto.Browser,
                    Country = dto.Country,
                    City = dto.City,
                    FollowUpSent = false,
                    LastInteractionType = "Clicked",
                    SessionId = Guid.TryParse(dto.SessionId, out var sid) ? sid : (Guid?)null,
                    ThreadId = Guid.TryParse(dto.ThreadId, out var tid) ? tid : (Guid?)null,
                    CampaignId = dto.CampaignId,
                    CampaignSendLogId = dto.CampaignSendLogId
                };

                await _context.TrackingLogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to log CTA click");
            }
        }
        public async Task<TrackingLogDetailsDto?> GetLogDetailsAsync(Guid logId)
        {
            var tracking = await _context.TrackingLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == logId);

            if (tracking == null)
                return null;

            var contact = tracking.ContactId != null
                ? await _context.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tracking.ContactId)
                : null;

            var campaign = tracking.CampaignId != null
                ? await _context.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tracking.CampaignId)
                : null;

            var messageLog = tracking.MessageLogId != null
                ? await _context.MessageLogs.AsNoTracking().FirstOrDefaultAsync(m => m.Id == tracking.MessageLogId)
                : null;

            return new TrackingLogDetailsDto
            {
                Tracking = _mapper.Map<TrackingLogDto>(tracking),
                Contact = contact != null ? _mapper.Map<ContactDto>(contact) : null,
                Campaign = campaign != null ? _mapper.Map<CampaignDto>(campaign) : null,
                MessageLog = messageLog != null ? _mapper.Map<MessageLogDto>(messageLog) : null
            };
        }
        public async Task<List<TrackingLog>> GetFlowClickLogsAsync(Guid businessId)
        {
            return await _context.TrackingLogs
                .Where(x => x.BusinessId == businessId && x.SourceType == "cta-flow")
                .OrderByDescending(x => x.ClickedAt)
                .ToListAsync();
        }
    }
}
