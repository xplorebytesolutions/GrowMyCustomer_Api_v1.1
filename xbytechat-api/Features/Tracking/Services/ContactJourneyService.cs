using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.Tracking.DTOs;

namespace xbytechat.api.Features.Tracking.Services
{
    public class ContactJourneyService : IContactJourneyService
    {
        private readonly AppDbContext _context;

        public ContactJourneyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<JourneyResponseDto> GetJourneyEventsAsync(Guid initialCampaignSendLogId, CancellationToken ct = default)
        {
            var resp = new JourneyResponseDto { Events = new List<JourneyEventDto>() };
            var events = resp.Events;

            // 0) Load the selected send (campaign required; contact optional)
            var sentLog = await _context.CampaignSendLogs
                .AsNoTracking()
                .Include(x => x.Campaign)
                .Include(x => x.Contact)
                .FirstOrDefaultAsync(x => x.Id == initialCampaignSendLogId, ct);

            if (sentLog is null || sentLog.Campaign is null)
                return resp;

            var campaignId = sentLog.CampaignId;
            resp.CampaignId = campaignId;
            resp.CampaignType = sentLog.CTAFlowConfigId.HasValue ? "flow" : "dynamic_url";
            resp.FlowId = sentLog.CTAFlowConfigId;

            if (sentLog.ContactId.HasValue)
                resp.ContactId = sentLog.ContactId.Value;

            // ---- Resolve a phone for display/flow fallback --------------------------------------------
            string? phone = sentLog.Contact?.PhoneNumber;

            // via MessageLog
            if (string.IsNullOrWhiteSpace(phone) && sentLog.MessageLogId.HasValue)
            {
                phone = await _context.MessageLogs.AsNoTracking()
                    .Where(m => m.Id == sentLog.MessageLogId.Value && m.BusinessId == sentLog.BusinessId)
                    .Select(m => m.RecipientNumber)
                    .FirstOrDefaultAsync(ct);
            }

            // via Recipient → Contact or AudienceMember
            if (string.IsNullOrWhiteSpace(phone) && sentLog.RecipientId != Guid.Empty)
            {
                var rec = await _context.CampaignRecipients.AsNoTracking()
                    .Where(r => r.Id == sentLog.RecipientId)
                    .Select(r => new { r.ContactId, r.AudienceMemberId })
                    .FirstOrDefaultAsync(ct);

                if (rec is not null)
                {
                    if (rec.ContactId.HasValue)
                        phone = await _context.Contacts.AsNoTracking()
                            .Where(c => c.Id == rec.ContactId.Value)
                            .Select(c => c.PhoneNumber)
                            .FirstOrDefaultAsync(ct);
                    else if (rec.AudienceMemberId.HasValue)
                        phone = await _context.AudienceMembers.AsNoTracking()
                            .Where(a => a.Id == rec.AudienceMemberId.Value)
                            .Select(a => a.PhoneE164)
                            .FirstOrDefaultAsync(ct);
                }
            }

            resp.ContactPhone = phone ?? "";

            // ---- 1) Session window ---------------------------------------------------------------------
            var sessionStart = sentLog.SentAt ?? sentLog.CreatedAt;
            DateTime sessionEnd;

            if (sentLog.ContactId.HasValue)
            {
                var contactId = sentLog.ContactId.Value;

                var nextSameCampaignAt = await _context.CampaignSendLogs.AsNoTracking()
                    .Where(x => x.ContactId == contactId &&
                                x.CampaignId == campaignId &&
                                x.CreatedAt > sessionStart)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => (DateTime?)x.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                DateTime? nextSameFlowAt = null;
                if (sentLog.CTAFlowConfigId.HasValue)
                {
                    var flowId = sentLog.CTAFlowConfigId.Value;
                    nextSameFlowAt = await _context.CampaignSendLogs.AsNoTracking()
                        .Where(x => x.ContactId == contactId &&
                                    x.CTAFlowConfigId == flowId &&
                                    x.CreatedAt > sessionStart)
                        .OrderBy(x => x.CreatedAt)
                        .Select(x => (DateTime?)x.CreatedAt)
                        .FirstOrDefaultAsync(ct);
                }

                sessionEnd = new[] { nextSameCampaignAt, nextSameFlowAt }
                    .Where(dt => dt.HasValue)
                    .Select(dt => dt!.Value)
                    .DefaultIfEmpty(sessionStart.AddHours(24))
                    .Min();
            }
            else
            {
                // No ContactId: keep it simple and robust
                sessionEnd = sessionStart.AddHours(24);
            }

            // ---- 2) Initial "sent" + statuses from CSL -------------------------------------------------
            events.Add(new JourneyEventDto
            {
                Timestamp = sessionStart,
                Source = "System",
                EventType = "MessageSent",
                Title = $"Campaign '{sentLog.Campaign?.Name ?? "Campaign"}' sent",
                Details = string.IsNullOrWhiteSpace(resp.ContactPhone) ? null :
                               $"Template '{sentLog.TemplateId}' to {resp.ContactPhone}",
                TemplateName = sentLog.TemplateId
            });

            if (sentLog.DeliveredAt is { } d1 && d1 >= sessionStart && d1 < sessionEnd)
                events.Add(new JourneyEventDto
                {
                    Timestamp = d1,
                    Source = "Provider",
                    EventType = "Delivered",
                    Title = "Message delivered",
                    Details = string.IsNullOrWhiteSpace(resp.ContactPhone) ? null : $"Delivered to {resp.ContactPhone}",
                    TemplateName = sentLog.TemplateId
                });

            if (sentLog.ReadAt is { } r1 && r1 >= sessionStart && r1 < sessionEnd)
                events.Add(new JourneyEventDto
                {
                    Timestamp = r1,
                    Source = "Provider",
                    EventType = "Read",
                    Title = "Message read",
                    Details = string.IsNullOrWhiteSpace(resp.ContactPhone) ? null : $"Read by {resp.ContactPhone}",
                    TemplateName = sentLog.TemplateId
                });

            // ---- 3) URL clicks for THIS send within the window ----------------------------------------
            var urlClicksInitial = await _context.CampaignClickLogs
                .AsNoTracking()
                .Where(c => c.CampaignSendLogId == sentLog.Id &&
                            c.ClickedAt >= sessionStart && c.ClickedAt < sessionEnd)
                .OrderBy(c => c.ClickedAt)
                .ToListAsync(ct);

            foreach (var c in urlClicksInitial)
            {
                events.Add(new JourneyEventDto
                {
                    Timestamp = c.ClickedAt,
                    Source = "User",
                    EventType = "Redirect",
                    Title = $"Clicked URL: '{c.ButtonTitle}'",
                    Details = $"Redirected to {c.Destination}",
                    ButtonIndex = c.ButtonIndex,
                    ButtonTitle = c.ButtonTitle,
                    Url = c.Destination
                });
            }

            // ---- 4) FLOW chain & clicks — robust against EF translation bugs --------------------------
            var flowEvents = new List<JourneyEventDto>(8);
            Guid? detectedFlowId = sentLog.CTAFlowConfigId;

            try
            {
                // Helper to limit to window and business
                IQueryable<FlowExecutionLog> Window(IQueryable<FlowExecutionLog> q) =>
                    q.AsNoTracking()
                     .Where(f => f.BusinessId == sentLog.BusinessId &&
                                 f.ExecutedAt >= sessionStart && f.ExecutedAt < sessionEnd);

                // Helper to project only needed columns (keeps SQL simple)
                IQueryable<FlowExecutionLog> Pick(IQueryable<FlowExecutionLog> q) =>
                    q.Select(f => new FlowExecutionLog
                    {
                        Id = f.Id,
                        FlowId = f.FlowId,
                        ExecutedAt = f.ExecutedAt,
                        CampaignSendLogId = f.CampaignSendLogId,
                        MessageLogId = f.MessageLogId,
                        StepId = f.StepId,
                        StepName = f.StepName,
                        TemplateName = f.TemplateName,
                        TriggeredByButton = f.TriggeredByButton,
                        ButtonIndex = f.ButtonIndex,
                        BusinessId = f.BusinessId,
                        ContactPhone = f.ContactPhone,
                        RunId = f.RunId
                    });

                // A) Prefer RunId when present
                var qRun = Window(_context.FlowExecutionLogs);
                if (sentLog.RunId != null)
                    qRun = qRun.Where(f => f.RunId == sentLog.RunId);

                // B) Always include direct references to this CSL/MessageLog
                var qDirect = Window(_context.FlowExecutionLogs)
                    .Where(f =>
                        (f.CampaignSendLogId.HasValue && f.CampaignSendLogId.Value == sentLog.Id) ||
                        (sentLog.MessageLogId.HasValue && f.MessageLogId == sentLog.MessageLogId));

                // C) Fallback: phone forms (+E.164 / digits only)
                IQueryable<FlowExecutionLog> qPhone = Enumerable.Empty<FlowExecutionLog>().AsQueryable();
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var digits = new string(phone.Where(char.IsDigit).ToArray());
                    var alt = phone.StartsWith("+") ? digits : "+" + digits;
                    qPhone = Window(_context.FlowExecutionLogs)
                        .Where(f => f.ContactPhone == phone ||
                                    f.ContactPhone == digits ||
                                    f.ContactPhone == alt);
                }

                // Materialize each branch separately (avoid EF's problematic GroupBy + Concat translation)
                var listRun = await Pick(qRun).OrderBy(f => f.ExecutedAt).ToListAsync(ct);
                var listDirect = await Pick(qDirect).OrderBy(f => f.ExecutedAt).ToListAsync(ct);
                var listPhone = await Pick(qPhone).OrderBy(f => f.ExecutedAt).ToListAsync(ct);

                // Merge & distinct in-
                // 
                var felAll = listRun
                    .Concat(listDirect)
                    .Concat(listPhone)
                    .GroupBy(f => f.Id)
                    .Select(g => g.First())
                    .OrderBy(f => f.ExecutedAt)
                    .ToList();

                if (felAll.Count > 0)
                    detectedFlowId ??= felAll.FirstOrDefault()?.FlowId;

                // If we found any flow events but original send looked Dynamic URL, upgrade the envelope
                if (felAll.Count > 0 && !sentLog.CTAFlowConfigId.HasValue)
                {
                    resp.CampaignType = "flow";
                    resp.FlowId = detectedFlowId;
                }

                if (resp.FlowId.HasValue && string.IsNullOrWhiteSpace(resp.FlowName))
                {
                    resp.FlowName = await _context.CTAFlowConfigs.AsNoTracking()
                        .Where(f => f.Id == resp.FlowId.Value)
                        .Select(f => f.FlowName)
                        .FirstOrDefaultAsync(ct);
                }

                foreach (var fe in felAll)
                {
                    if (!string.IsNullOrWhiteSpace(fe.TriggeredByButton))
                    {
                        flowEvents.Add(new JourneyEventDto
                        {
                            Timestamp = fe.ExecutedAt,
                            Source = "User",
                            EventType = "ButtonClicked",
                            Title = $"Clicked Quick Reply: '{fe.TriggeredByButton}'",
                            Details = string.IsNullOrWhiteSpace(fe.TemplateName)
                                        ? (string.IsNullOrWhiteSpace(fe.StepName) ? null : $"Advanced in flow at step '{fe.StepName}'")
                                        : $"Triggered next template: '{fe.TemplateName}'",
                            StepId = fe.StepId,
                            StepName = fe.StepName,
                            ButtonIndex = fe.ButtonIndex,
                            ButtonTitle = fe.TriggeredByButton,
                            TemplateName = fe.TemplateName
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(fe.TemplateName))
                    {
                        flowEvents.Add(new JourneyEventDto
                        {
                            Timestamp = fe.ExecutedAt,
                            Source = "System",
                            EventType = "FlowSend",
                            Title = $"Flow sent template '{fe.TemplateName}'",
                            Details = string.IsNullOrWhiteSpace(fe.StepName) ? null : $"Step '{fe.StepName}'",
                            StepId = fe.StepId,
                            StepName = fe.StepName,
                            TemplateName = fe.TemplateName
                        });
                    }
                }

                // Also include URL clicks that happened during the flow window across the chain
                if (felAll.Count > 0)
                {
                    var cslIdsFromFel = felAll
                        .Where(f => f.CampaignSendLogId.HasValue)
                        .Select(f => f.CampaignSendLogId!.Value)
                        .Distinct()
                        .ToList();

                    if (cslIdsFromFel.Count > 0)
                    {
                        var flowClicks = await _context.CampaignClickLogs.AsNoTracking()
                            .Where(c => c.ClickedAt >= sessionStart &&
                                        c.ClickedAt < sessionEnd &&
                                        cslIdsFromFel.Contains(c.CampaignSendLogId))
                            .OrderBy(c => c.ClickedAt)
                            .ToListAsync(ct);

                        foreach (var c in flowClicks)
                        {
                            flowEvents.Add(new JourneyEventDto
                            {
                                Timestamp = c.ClickedAt,
                                Source = "User",
                                EventType = "Redirect",
                                Title = $"Clicked URL: '{c.ButtonTitle}'",
                                Details = $"Redirected to {c.Destination}",
                                ButtonIndex = c.ButtonIndex,
                                ButtonTitle = c.ButtonTitle,
                                Url = c.Destination
                            });
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException)
            {
                // Defensive: never bubble the EF translation issue; return what we have.
                // You can log here if you have a logger: _logger.LogWarning(ex, "Flow merge failed");
            }

            events.AddRange(flowEvents);

            // Left-off marker (last flow action)
            var lastFlowEvent = flowEvents.OrderBy(e => e.Timestamp).LastOrDefault();
            resp.LeftOffAt = lastFlowEvent?.StepName ?? lastFlowEvent?.Title;

            // ---- Final ordering ------------------------------------------------------------------------
            resp.Events = events.OrderBy(e => e.Timestamp).ToList();
            return resp;
        }
    }
}
