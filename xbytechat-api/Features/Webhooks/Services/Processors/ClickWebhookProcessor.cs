using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading.Tasks;
using xbytechat.api;
using xbytechat.api.DTOs.Messages;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Services;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.CTAFlowBuilder.Services;
using xbytechat.api.Features.CustomeApi.Models;
using xbytechat.api.Features.CustomeApi.Services;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Features.Tracking.Models;
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.Features.Webhooks.Services.Resolvers;
using xbytechat.api.Helpers;
using xbytechat.api.Shared.TrackingUtils;

namespace xbytechat.api.Features.Webhooks.Services.Processors
{
    public class ClickWebhookProcessor : IClickWebhookProcessor
    {
        private readonly ILogger<ClickWebhookProcessor> _logger;
        private readonly IMessageIdResolver _messageIdResolver;
        private readonly ITrackingService _trackingService;
        private readonly AppDbContext _context;
        private readonly IMessageEngineService _messageEngine;
        private readonly ICTAFlowService _flowService;
        private readonly IFlowRuntimeService _flowRuntime;
        private readonly IContactProfileService _contactProfile;
        private readonly ICtaJourneyPublisher _journeyPublisher;
        public ClickWebhookProcessor(
            ILogger<ClickWebhookProcessor> logger,
            IMessageIdResolver messageIdResolver,
            ITrackingService trackingService,
            AppDbContext context,
            IMessageEngineService messageEngine,
            ICTAFlowService flowService,
                        IFlowRuntimeService flowRuntime,
                         IContactProfileService contactProfile,
                          ICtaJourneyPublisher journeyPublisher
            )
        {
            _logger = logger;
            _messageIdResolver = messageIdResolver;
            _trackingService = trackingService;
            _context = context;
            _messageEngine = messageEngine;
            _flowService = flowService;
            _flowRuntime = flowRuntime;
            _contactProfile = contactProfile;
            _journeyPublisher = journeyPublisher;

        }

        // working code

        //public async Task ProcessClickAsync(JsonElement value)
        //{
        //    _logger.LogWarning("📥 [ENTERED CLICK PROCESSOR]");

        //    try
        //    {
        //        if (!value.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0)
        //            return;

        //        static string Norm(string? s)
        //        {
        //            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        //            return string.Join(' ', s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        //                         .Trim()
        //                         .ToLowerInvariant();
        //        }

        //        // ✅ Canonical phone: keep only digits (matches how we store & search contacts)
        //        static string NormalizePhone(string? raw)
        //            => new string((raw ?? string.Empty).Where(char.IsDigit).ToArray());

        //        // ✅ contacts[0].profile.name (Meta shape)
        //        static string? TryGetProfileName(JsonElement root)
        //        {
        //            if (root.TryGetProperty("contacts", out var contactsEl) &&
        //                contactsEl.ValueKind == JsonValueKind.Array &&
        //                contactsEl.GetArrayLength() > 0)
        //            {
        //                var c0 = contactsEl[0];
        //                if (c0.TryGetProperty("profile", out var profEl) &&
        //                    profEl.ValueKind == JsonValueKind.Object &&
        //                    profEl.TryGetProperty("name", out var nameEl) &&
        //                    nameEl.ValueKind == JsonValueKind.String)
        //                {
        //                    var n = nameEl.GetString();
        //                    return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
        //                }
        //            }
        //            return null;
        //        }

        //        // >>> BEGIN MOD: helpers for CTAJourney key + botId extraction
        //        static string ToKey(string? s)
        //        {
        //            if (string.IsNullOrWhiteSpace(s)) return "unknown";
        //            var t = s.Trim().ToLowerInvariant();
        //            var sb = new System.Text.StringBuilder(t.Length);
        //            foreach (var ch in t)
        //            {
        //                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        //                else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_' || ch == '.') sb.Append('_');
        //            }
        //            var k = sb.ToString().Trim('_');
        //            return string.IsNullOrEmpty(k) ? "unknown" : k;
        //        }

        //        // read WA display number once (used as botId)
        //        string botIdFromWebhook = "";
        //        if (value.TryGetProperty("metadata", out var md) &&
        //            md.TryGetProperty("display_phone_number", out var dpnEl) &&
        //            dpnEl.ValueKind == JsonValueKind.String)
        //        {
        //            botIdFromWebhook = NormalizePhone(dpnEl.GetString());
        //        }
        //        // >>> END MOD

        //        foreach (var msg in messages.EnumerateArray())
        //        {
        //            if (!msg.TryGetProperty("type", out var typeProp))
        //                continue;

        //            var type = typeProp.GetString();

        //            string? clickMessageId = msg.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        //            string? originalMessageId = msg.TryGetProperty("context", out var ctx) && ctx.TryGetProperty("id", out var ctxId)
        //                ? ctxId.GetString()
        //                : null;
        //            var fromRaw = msg.TryGetProperty("from", out var fromProp) ? (fromProp.GetString() ?? "") : "";
        //            var fromDigits = NormalizePhone(fromRaw);

        //            // ——— button label extraction
        //            string? buttonText = null;
        //            if (string.Equals(type, "button", StringComparison.OrdinalIgnoreCase))
        //            {
        //                buttonText = msg.TryGetProperty("button", out var btn) &&
        //                             btn.TryGetProperty("text", out var textProp)
        //                               ? textProp.GetString()?.Trim()
        //                               : null;
        //            }
        //            else if (string.Equals(type, "interactive", StringComparison.OrdinalIgnoreCase) &&
        //                     msg.TryGetProperty("interactive", out var interactive))
        //            {
        //                if (interactive.TryGetProperty("type", out var intrType) &&
        //                    string.Equals(intrType.GetString(), "button_reply", StringComparison.OrdinalIgnoreCase) &&
        //                    interactive.TryGetProperty("button_reply", out var br) &&
        //                    br.TryGetProperty("title", out var titleProp))
        //                {
        //                    buttonText = titleProp.GetString()?.Trim();
        //                }
        //                else if (interactive.TryGetProperty("list_reply", out var lr) &&
        //                         lr.TryGetProperty("title", out var listTitleProp))
        //                {
        //                    buttonText = listTitleProp.GetString()?.Trim();
        //                }
        //            }

        //            if (string.IsNullOrWhiteSpace(buttonText) || string.IsNullOrWhiteSpace(originalMessageId))
        //            {
        //                _logger.LogDebug("ℹ️ Not a recognized click or missing context.id. type={Type}", type);
        //                continue;
        //            }

        //            _logger.LogInformation("🖱️ Button Click → From: {From}, ClickId: {ClickId}, OrigMsgId: {OrigId}, Text: {Text}",
        //                fromDigits, clickMessageId, originalMessageId, buttonText);

        //            // —— Try 1: originating MessageLog (for flow-sent messages)
        //            var origin = await _context.MessageLogs
        //                .AsNoTracking()
        //                .FirstOrDefaultAsync(m =>
        //                    m.MessageId == originalMessageId &&
        //                    m.CTAFlowConfigId != null &&
        //                    m.CTAFlowStepId != null);

        //            Guid businessId;
        //            Guid flowId;
        //            Guid stepId;
        //            string? bundleJson = null;
        //            int? flowVersion = null;

        //            Guid? campaignSendLogId = null; // link the click to the shown message
        //            Guid? runId = null;             // copy from parent CSL when available

        //            if (origin != null)
        //            {
        //                businessId = origin.BusinessId;
        //                flowId = origin.CTAFlowConfigId!.Value;
        //                stepId = origin.CTAFlowStepId!.Value;
        //                bundleJson = origin.ButtonBundleJson;
        //                flowVersion = origin.FlowVersion;

        //                // Map back to CSL via MessageLogId or WAMID and fetch RunId
        //                var cslInfo = await _context.CampaignSendLogs
        //                    .AsNoTracking()
        //                    .Where(csl => (csl.MessageLogId == origin.Id) || (csl.MessageId == originalMessageId))
        //                    .OrderByDescending(csl => csl.CreatedAt)
        //                    .Select(csl => new { csl.Id, csl.RunId })
        //                    .FirstOrDefaultAsync();

        //                campaignSendLogId = cslInfo?.Id;
        //                runId = cslInfo?.RunId;
        //            }
        //            else
        //            {
        //                // —— Try 2: first campaign message (CampaignSendLogs)
        //                var sendLog = await _context.CampaignSendLogs
        //                    .Include(sl => sl.Campaign)
        //                    .AsNoTracking()
        //                    .FirstOrDefaultAsync(sl => sl.MessageId == originalMessageId);

        //                if (sendLog == null)
        //                {
        //                    _logger.LogWarning("❌ No MessageLog or CampaignSendLog for original WAMID {Orig}", originalMessageId);
        //                    continue;
        //                }

        //                businessId = sendLog.BusinessId != Guid.Empty
        //                    ? sendLog.BusinessId
        //                    : (sendLog.Campaign?.BusinessId ?? Guid.Empty);

        //                if (businessId == Guid.Empty)
        //                {
        //                    _logger.LogWarning("❌ Could not resolve BusinessId for WAMID {Orig}", originalMessageId);
        //                    continue;
        //                }

        //                campaignSendLogId = sendLog.Id;
        //                runId = sendLog.RunId;

        //                if (sendLog.CTAFlowConfigId.HasValue && sendLog.CTAFlowStepId.HasValue)
        //                {
        //                    flowId = sendLog.CTAFlowConfigId.Value;
        //                    stepId = sendLog.CTAFlowStepId.Value;
        //                }
        //                else if (sendLog.Campaign?.CTAFlowConfigId != null)
        //                {
        //                    flowId = sendLog.Campaign.CTAFlowConfigId.Value;

        //                    var entry = await _context.CTAFlowSteps
        //                        .Where(s => s.CTAFlowConfigId == flowId)
        //                        .OrderBy(s => s.StepOrder)
        //                        .Select(s => s.Id)
        //                        .FirstOrDefaultAsync();

        //                    if (entry == Guid.Empty)
        //                    {
        //                        _logger.LogWarning("❌ No entry step found for flow {Flow}", flowId);
        //                        continue;
        //                    }

        //                    stepId = entry;
        //                }
        //                else
        //                {
        //                    _logger.LogWarning("❌ No flow context on CampaignSendLog for WAMID {Orig}", originalMessageId);
        //                    continue;
        //                }

        //                bundleJson = sendLog.ButtonBundleJson;
        //            }

        //            // ─────────────────────────────────────────────────────────────
        //            // ✅ UPSERT PROFILE NAME (create-or-update) *before* next step
        //            //    and make sure we look up by digits-only phone.
        //            // ─────────────────────────────────────────────────────────────
        //            try
        //            {
        //                var profileName = TryGetProfileName(value);
        //                if (!string.IsNullOrWhiteSpace(profileName))
        //                {
        //                    var now = DateTime.UtcNow;
        //                    var contact = await _context.Contacts
        //                        .FirstOrDefaultAsync(c => c.BusinessId == businessId &&
        //                                                  (c.PhoneNumber == fromDigits || c.PhoneNumber == fromRaw));

        //                    if (contact == null)
        //                    {
        //                        profileName = profileName ?? "User";
        //                        contact = new Contact
        //                        {
        //                            Id = Guid.NewGuid(),
        //                            BusinessId = businessId,
        //                            PhoneNumber = fromDigits, // store canonical
        //                            Name = profileName,
        //                            ProfileName = profileName,
        //                            ProfileNameUpdatedAt = now,
        //                            CreatedAt = now,
        //                        };
        //                        _context.Contacts.Add(contact);
        //                        await _context.SaveChangesAsync();
        //                        _logger.LogInformation("👤 Created contact + stored WA profile '{Name}' for {Phone} (biz {Biz})",
        //                            profileName, fromDigits, businessId);
        //                    }
        //                    else
        //                    {
        //                        var changed = false;

        //                        if (!string.Equals(contact.ProfileName, profileName, StringComparison.Ordinal))
        //                        {
        //                            contact.ProfileName = profileName;
        //                            contact.ProfileNameUpdatedAt = now;
        //                            changed = true;
        //                        }

        //                        if (string.IsNullOrWhiteSpace(contact.Name) ||
        //                            contact.Name == "WhatsApp User" ||
        //                            contact.Name == contact.PhoneNumber)
        //                        {
        //                            if (!string.Equals(contact.Name, profileName, StringComparison.Ordinal))
        //                            {
        //                                contact.Name = profileName;
        //                                changed = true;
        //                            }
        //                        }

        //                        if (changed)
        //                        {
        //                            contact.ProfileNameUpdatedAt = now;
        //                            await _context.SaveChangesAsync();
        //                            _logger.LogInformation("👤 Updated WA profile name to '{Name}' for {Phone} (biz {Biz})",
        //                                profileName, fromDigits, businessId);
        //                        }
        //                    }
        //                }
        //            }
        //            catch (Exception exProf)
        //            {
        //                _logger.LogWarning(exProf, "⚠️ Failed to upsert WA profile name on click webhook.");
        //            }

        //            // —— Map clicked text -> button index via the shown bundle
        //            short? buttonIndex = null;
        //            FlowBtnBundleNode? hit = null;

        //            if (!string.IsNullOrWhiteSpace(bundleJson))
        //            {
        //                try
        //                {
        //                    var nodes = System.Text.Json.JsonSerializer
        //                        .Deserialize<List<FlowBtnBundleNode>>(bundleJson) ?? new();

        //                    hit = nodes.FirstOrDefault(n =>
        //                              string.Equals(n.t ?? "", buttonText, StringComparison.OrdinalIgnoreCase))
        //                          ?? nodes.FirstOrDefault(n => Norm(n.t) == Norm(buttonText));

        //                    if (hit != null)
        //                        buttonIndex = (short)hit.i;
        //                }
        //                catch (Exception ex)
        //                {
        //                    _logger.LogWarning(ex, "⚠️ Failed to parse ButtonBundleJson");
        //                }
        //            }

        //            // —— Fallback: find link by TEXT for this step
        //            FlowButtonLink? linkMatchedByText = null;
        //            if (buttonIndex == null)
        //            {
        //                var stepLinks = await _context.FlowButtonLinks
        //                    .Where(l => l.CTAFlowStepId == stepId)
        //                    .OrderBy(l => l.ButtonIndex)
        //                    .ToListAsync();

        //                if (stepLinks.Count > 0)
        //                {
        //                    linkMatchedByText = stepLinks.FirstOrDefault(l =>
        //                        string.Equals(l.ButtonText ?? "", buttonText, StringComparison.OrdinalIgnoreCase))
        //                        ?? stepLinks.FirstOrDefault(l => Norm(l.ButtonText) == Norm(buttonText));

        //                    if (linkMatchedByText == null && stepLinks.Count == 1)
        //                    {
        //                        linkMatchedByText = stepLinks[0];
        //                        _logger.LogInformation("🟨 Falling back to single available link for step {Step}", stepId);
        //                    }

        //                    if (linkMatchedByText != null)
        //                    {
        //                        buttonIndex = (short?)linkMatchedByText.ButtonIndex;
        //                        _logger.LogInformation("✅ Mapped click by TEXT to index {Idx} (flow={Flow}, step={Step})",
        //                            buttonIndex, flowId, stepId);
        //                    }
        //                }
        //            }

        //            if (buttonIndex == null)
        //            {
        //                _logger.LogInformation("🟡 Button text not found in bundle or flow links. Ref={Ref}, Text='{Text}'",
        //                    originalMessageId, buttonText);
        //                continue;
        //            }

        //            // —— Prefer exact link by index; otherwise use the text-matched link
        //            var link = await _flowService.GetLinkAsync(flowId, stepId, buttonIndex.Value)
        //                       ?? linkMatchedByText;

        //            if (link == null)
        //            {
        //                _logger.LogInformation("🟡 No button link for (flow={Flow}, step={Step}, idx={Idx})",
        //                    flowId, stepId, buttonIndex);
        //                continue;
        //            }

        //            // —— Resolve index + step name (for logging)
        //            short resolvedIndex = buttonIndex ?? Convert.ToInt16(link.ButtonIndex);
        //            var stepName = await _context.CTAFlowSteps
        //                .Where(s => s.Id == stepId)
        //                .Select(s => s.TemplateToSend)
        //                .FirstOrDefaultAsync() ?? string.Empty;

        //            // ————————————————
        //            // 📝 WRITE CLICK LOG (always, even if terminal)
        //            // ————————————————
        //            try
        //            {
        //                var clickExec = new FlowExecutionLog
        //                {
        //                    Id = Guid.NewGuid(),
        //                    BusinessId = businessId,
        //                    FlowId = flowId,
        //                    StepId = stepId,
        //                    StepName = stepName,
        //                    CampaignSendLogId = campaignSendLogId,
        //                    MessageLogId = origin?.Id,
        //                    ContactPhone = fromDigits,      // ✅ digits-only, consistent
        //                    ButtonIndex = resolvedIndex,
        //                    TriggeredByButton = buttonText,
        //                    TemplateName = null,
        //                    TemplateType = "quick_reply",
        //                    Success = true,
        //                    ExecutedAt = DateTime.UtcNow,
        //                    RequestId = Guid.NewGuid(),
        //                    RunId = runId
        //                };

        //                _context.FlowExecutionLogs.Add(clickExec);
        //                await _context.SaveChangesAsync();
        //            }
        //            catch (Exception exSave)
        //            {
        //                _logger.LogWarning(exSave, "⚠️ Failed to persist FlowExecutionLog (click). Continuing…");
        //            }
        //            // ===== RUNNING CTA JOURNEY STATE UPSERT (ONLY IF THIS BIZ IS CONFIGURED) =====
        //            string runningJourney;

        //            // Check once if this business is configured to receive CTAJourney.
        //            // If not, we won't touch ContactJourneyStates at all.
        //            bool shouldTrackState = await _context.CustomerWebhookConfigs
        //                .AsNoTracking()
        //                .AnyAsync(x => x.BusinessId == businessId && x.IsActive);

        //            if (shouldTrackState)
        //            {
        //                try
        //                {
        //                    // load current state for (business, flow, phone)
        //                    var state = await _context.ContactJourneyStates
        //                        .SingleOrDefaultAsync(s =>
        //                            s.BusinessId == businessId &&
        //                            s.FlowId == flowId &&
        //                            s.ContactPhone == fromDigits);

        //                    if (state == null)
        //                    {
        //                        // first click -> start with this button text (original casing)
        //                        state = new ContactJourneyState
        //                        {
        //                            Id = Guid.NewGuid(),
        //                            BusinessId = businessId,
        //                            FlowId = flowId,
        //                            ContactPhone = fromDigits,
        //                            JourneyText = buttonText ?? string.Empty,
        //                            ClickCount = 1,
        //                            LastButtonText = buttonText,
        //                            CreatedAt = DateTime.UtcNow,
        //                            UpdatedAt = DateTime.UtcNow
        //                        };
        //                        _context.ContactJourneyStates.Add(state);
        //                        await _context.SaveChangesAsync();
        //                        runningJourney = state.JourneyText;
        //                        _logger.LogInformation("🧵 Journey init: {Journey} (biz={Biz}, flow={Flow}, phone={Phone})",
        //                            runningJourney, businessId, flowId, fromDigits);
        //                    }
        //                    else
        //                    {
        //                        // append EVERY press (duplicates allowed), keep original casing
        //                        var parts = (state.JourneyText ?? string.Empty)
        //                            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        //                            .ToList();

        //                        if (!string.IsNullOrWhiteSpace(buttonText))
        //                            parts.Add(buttonText!);

        //                        // optional safety: cap growth
        //                        const int cap = 15;
        //                        if (parts.Count > cap) parts = parts.Skip(parts.Count - cap).ToList();

        //                        state.JourneyText = string.Join('/', parts);
        //                        state.ClickCount += 1;
        //                        state.LastButtonText = buttonText;
        //                        state.UpdatedAt = DateTime.UtcNow;

        //                        await _context.SaveChangesAsync();
        //                        runningJourney = state.JourneyText ?? string.Empty;

        //                        _logger.LogInformation("🧵 Journey update: {Journey} (biz={Biz}, flow={Flow}, phone={Phone})",
        //                            runningJourney, businessId, flowId, fromDigits);
        //                    }
        //                }
        //                catch (Exception exState)
        //                {
        //                    _logger.LogWarning(exState, "⚠️ Failed to upsert ContactJourneyState.");
        //                    // fall back to this click only
        //                    runningJourney = buttonText ?? string.Empty;
        //                }
        //            }
        //            else
        //            {
        //                // Business not configured → do NOT save any state. Just use the current button for emit.
        //                runningJourney = buttonText ?? string.Empty;
        //            }
        //            // ===== END RUNNING CTA JOURNEY STATE UPSERT =====


        //            // ===== CTAJourney EMIT (running journey) =====
        //            try
        //            {
        //                // contact (for userName / userPhone)
        //                var contact = await _context.Contacts
        //                    .AsNoTracking()
        //                    .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == fromDigits);

        //                // prefer PhoneNumberId (botId) from the originating send; otherwise pick any active one
        //                string? phoneNumberId = null;
        //                if (campaignSendLogId.HasValue)
        //                {
        //                    phoneNumberId = await _context.CampaignSendLogs
        //                        .AsNoTracking()
        //                        .Where(s => s.Id == campaignSendLogId.Value)
        //                        .Select(s => s.Campaign.PhoneNumberId)
        //                        .FirstOrDefaultAsync();
        //                }
        //                if (string.IsNullOrWhiteSpace(phoneNumberId) && origin?.CampaignId != null)
        //                {
        //                    phoneNumberId = await _context.Campaigns
        //                        .AsNoTracking()
        //                        .Where(c => c.Id == origin.CampaignId.Value)
        //                        .Select(c => c.PhoneNumberId)
        //                        .FirstOrDefaultAsync();
        //                }

        //                //if (string.IsNullOrWhiteSpace(phoneNumberId))
        //                //{
        //                //    phoneNumberId = await _context.WhatsAppSettings
        //                //        .AsNoTracking()
        //                //        .Where(s => s.BusinessId == businessId && s.IsActive && s.PhoneNumberId != null)
        //                //        .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
        //                //        .Select(s => s.PhoneNumberId)
        //                //        .FirstOrDefaultAsync();
        //                //}
        //                // 2) Map PhoneNumberId -> WhatsAppBusinessNumber
        //                string? botWaNumber = null;
        //                if (!string.IsNullOrWhiteSpace(phoneNumberId))
        //                {
        //                    botWaNumber = await _context.WhatsAppPhoneNumbers
        //                        .AsNoTracking()
        //                        .Where(n => n.BusinessId == businessId && n.PhoneNumberId == phoneNumberId)
        //                        .Select(n => n.WhatsAppBusinessNumber)
        //                        .FirstOrDefaultAsync();
        //                }
        //                // business WA display number (fallback botId if no PhoneNumberId)
        //                var displayProfilename = await _context.WhatsAppSettings
        //                    .AsNoTracking()
        //                    .Where(s => s.BusinessId == businessId && s.IsActive && s.WhatsAppBusinessNumber != null)
        //                    .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
        //                    .Select(s => s.WhatsAppBusinessNumber)
        //                    .FirstOrDefaultAsync();

        //                // Build DTO and POST (CTAJourney = the running slash-joined string with original casing)
        //                var dto = CtaJourneyMapper.Build(
        //                    journeyKey: runningJourney,                    // <<—— use the running state
        //                    contact: contact,
        //                    profileName: contact?.ProfileName ?? contact?.Name,
        //                    userId: null,
        //                    phoneNumberId: botWaNumber,                  // preferred botId
        //                    businessDisplayPhone: displayProfilename,               // fallback botId if above missing
        //                    categoryBrowsed: null,
        //                    productBrowsed: null
        //                );

        //                await _journeyPublisher.PublishAsync(businessId, dto, CancellationToken.None);
        //                _logger.LogInformation("📤 CTAJourney posted (running): {Journey} (biz={Biz}, phone={Phone})",
        //                    dto.CTAJourney, businessId, dto.userPhone);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogWarning(ex, "⚠️ Failed to post CTAJourney (click). Continuing…");
        //            }

        //            // ===== end CTAJourney EMIT =====


        //            // ===== CTAJourney EMIT (button name) =====
        //            //try
        //            //{
        //            //    CTAJourney must be the button name now
        //            //   var journeyKey = ToKey(buttonText);
        //            //    var journeyKey = buttonText?.Trim();
        //            //    contact(for userName / userPhone)
        //            //        var contact = await _context.Contacts
        //            //            .AsNoTracking()
        //            //            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == fromDigits);

        //            //    prefer PhoneNumberId(botId) from the originating send; otherwise pick any active one
        //            //    string? phoneNumberId = null;
        //            //    if (campaignSendLogId.HasValue)
        //            //    {
        //            //        phoneNumberId = await _context.CampaignSendLogs
        //            //            .AsNoTracking()
        //            //            .Where(s => s.Id == campaignSendLogId.Value)
        //            //            .Select(s => s.Campaign.PhoneNumberId)
        //            //            .FirstOrDefaultAsync();
        //            //    }
        //            //    if (string.IsNullOrWhiteSpace(phoneNumberId) && origin?.CampaignId != null)
        //            //    {
        //            //        phoneNumberId = await _context.Campaigns
        //            //            .AsNoTracking()
        //            //            .Where(c => c.Id == origin.CampaignId.Value)
        //            //            .Select(c => c.PhoneNumberId)
        //            //            .FirstOrDefaultAsync();
        //            //    }
        //            //    if (string.IsNullOrWhiteSpace(phoneNumberId))
        //            //    {
        //            //        phoneNumberId = await _context.WhatsAppSettings
        //            //            .AsNoTracking()
        //            //            .Where(s => s.BusinessId == businessId && s.IsActive && s.PhoneNumberId != null)
        //            //            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
        //            //            .Select(s => s.PhoneNumberId)
        //            //            .FirstOrDefaultAsync();
        //            //    }

        //            //    business WA display number(fallback botId if no PhoneNumberId)
        //            //    var displayWa = await _context.WhatsAppSettings
        //            //        .AsNoTracking()
        //            //        .Where(s => s.BusinessId == businessId && s.IsActive && s.WhatsAppBusinessNumber != null)
        //            //        .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
        //            //        .Select(s => s.WhatsAppBusinessNumber)
        //            //        .FirstOrDefaultAsync();

        //            //    build DTO and POST(maps to: userName / profileName, userPhone, botId, CTAJourney)
        //            //    var dto = CtaJourneyMapper.Build(
        //            //        journeyKey: journeyKey,                         // <<—— button name
        //            //        contact: contact,
        //            //        profileName: contact?.ProfileName ?? contact?.Name,
        //            //        userId: null,                                   // we don't have external user id
        //            //        phoneNumberId: phoneNumberId,                   // preferred botId
        //            //        businessDisplayPhone: displayWa,                // fallback botId if above missing
        //            //        categoryBrowsed: null,
        //            //        productBrowsed: null
        //            //    );

        //            //    await _journeyPublisher.PublishAsync(businessId, dto, CancellationToken.None);
        //            //    _logger.LogInformation("📤 CTAJourney posted (button): {Journey} (biz={Biz}, phone={Phone})",
        //            //        dto.CTAJourney, businessId, dto.userPhone);
        //            //}
        //            //catch (Exception ex)
        //            //{
        //            //    _logger.LogWarning(ex, "⚠️ Failed to post CTAJourney (click). Continuing…");
        //            //}





        //            // —— If terminal/URL button: already logged the click
        //            if (link.NextStepId == null)
        //            {
        //                _logger.LogInformation("🔚 Terminal/URL button: no NextStepId. flow={Flow}, step={Step}, idx={Idx}, text='{Text}'",
        //                    flowId, stepId, resolvedIndex, link.ButtonText);
        //                continue;
        //            }

        //            if (_flowRuntime == null)
        //            {
        //                _logger.LogError("❌ _flowRuntime is null. Cannot execute next step. flow={Flow}, step={Step}, idx={Idx}", flowId, stepId, resolvedIndex);
        //                continue;
        //            }

        //            // —— 🔎 Resolve sender from the originating campaign/send (use SAME WABA)
        //            string? providerFromCampaign = null;
        //            string? phoneNumberIdFromCampaign = null;

        //            if (campaignSendLogId.HasValue)
        //            {
        //                var originSend = await _context.CampaignSendLogs
        //                    .AsNoTracking()
        //                    .Include(s => s.Campaign)
        //                    .Where(s => s.Id == campaignSendLogId.Value)
        //                    .Select(s => new
        //                    {
        //                        s.Campaign.Provider,
        //                        s.Campaign.PhoneNumberId
        //                    })
        //                    .FirstOrDefaultAsync();

        //                providerFromCampaign = originSend?.Provider;
        //                phoneNumberIdFromCampaign = originSend?.PhoneNumberId;
        //            }
        //            else if (origin != null && origin.CampaignId.HasValue)
        //            {
        //                var originCamp = await _context.Campaigns
        //                    .AsNoTracking()
        //                    .Where(c => c.Id == origin.CampaignId.Value)
        //                    .Select(c => new { c.Provider, c.PhoneNumberId })
        //                    .FirstOrDefaultAsync();

        //                providerFromCampaign = originCamp?.Provider;
        //                phoneNumberIdFromCampaign = originCamp?.PhoneNumberId;
        //            }

        //            // —— Execute next (carry sender forward)
        //            var ctxObj = new NextStepContext
        //            {
        //                BusinessId = businessId,
        //                FlowId = flowId,
        //                Version = flowVersion ?? 1,
        //                SourceStepId = stepId,
        //                TargetStepId = link.NextStepId!.Value,
        //                ButtonIndex = resolvedIndex,
        //                MessageLogId = origin?.Id ?? Guid.Empty,
        //                ContactPhone = fromDigits,     // ✅ digits-only, so runtime finds the Contact
        //                RequestId = Guid.NewGuid(),
        //                ClickedButton = link,

        //                // 🧷 Sender from campaign so runtime won’t guess or fail with “Missing PhoneNumberId”
        //                Provider = providerFromCampaign,
        //                PhoneNumberId = phoneNumberIdFromCampaign,
        //                AlwaysSend = true // 🔥 force runtime to send even if it’s a loopback/same step
        //            };

        //            try
        //            {
        //                var result = await _flowRuntime.ExecuteNextAsync(ctxObj);

        //                if (result.Success && !string.IsNullOrWhiteSpace(result.RedirectUrl))
        //                {
        //                    _logger.LogInformation("🔗 URL button redirect (logical): {Url}", result.RedirectUrl);
        //                }
        //            }
        //            catch (Exception exRun)
        //            {
        //                _logger.LogError(exRun,
        //                    "❌ ExecuteNextAsync failed. ctx: flow={Flow} step={Step} next={Next} idx={Idx} from={From} orig={Orig} text='{Text}'",
        //                    ctxObj.FlowId, ctxObj.SourceStepId, ctxObj.TargetStepId, ctxObj.ButtonIndex, fromDigits, originalMessageId, buttonText);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "❌ Failed to process CTA button click.");
        //    }
        //}

        public async Task ProcessClickAsync(JsonElement value)
        {
            _logger.LogWarning("📥 [ENTERED CLICK PROCESSOR]");

            try
            {
                if (!value.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0)
                    return;

                // ── local helpers ─────────────────────────────────────────────────────────
                static string Norm(string? s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                    return string.Join(' ', s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                 .Trim()
                                 .ToLowerInvariant();
                }

                // canonical phone: keep only digits (matches how we store & search contacts)
                static string NormalizePhone(string? raw)
                    => new string((raw ?? string.Empty).Where(char.IsDigit).ToArray());

                // contacts[0].profile.name (Meta shape)
                static string? TryGetProfileName(JsonElement root)
                {
                    if (root.TryGetProperty("contacts", out var contactsEl) &&
                        contactsEl.ValueKind == JsonValueKind.Array &&
                        contactsEl.GetArrayLength() > 0)
                    {
                        var c0 = contactsEl[0];
                        if (c0.TryGetProperty("profile", out var profEl) &&
                            profEl.ValueKind == JsonValueKind.Object &&
                            profEl.TryGetProperty("name", out var nameEl) &&
                            nameEl.ValueKind == JsonValueKind.String)
                        {
                            var n = nameEl.GetString();
                            return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
                        }
                    }
                    return null;
                }

                // optional helper (kept for future use)
                static string ToKey(string? s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return "unknown";
                    var t = s.Trim().ToLowerInvariant();
                    var sb = new System.Text.StringBuilder(t.Length);
                    foreach (var ch in t)
                    {
                        if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                        else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_' || ch == '.') sb.Append('_');
                    }
                    var k = sb.ToString().Trim('_');
                    return string.IsNullOrEmpty(k) ? "unknown" : k;
                }
                // ──────────────────────────────────────────────────────────────────────────

                // read WA display number once (used as botId) – optional, not strictly required
                string botIdFromWebhook = "";
                if (value.TryGetProperty("metadata", out var md) &&
                    md.TryGetProperty("display_phone_number", out var dpnEl) &&
                    dpnEl.ValueKind == JsonValueKind.String)
                {
                    botIdFromWebhook = NormalizePhone(dpnEl.GetString());
                }

                foreach (var msg in messages.EnumerateArray())
                {
                    if (!msg.TryGetProperty("type", out var typeProp))
                        continue;

                    var type = typeProp.GetString();

                    string? clickMessageId = msg.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    string? originalMessageId = msg.TryGetProperty("context", out var ctx) && ctx.TryGetProperty("id", out var ctxId)
                        ? ctxId.GetString()
                        : null;

                    var fromRaw = msg.TryGetProperty("from", out var fromProp) ? (fromProp.GetString() ?? "") : "";
                    var fromDigits = NormalizePhone(fromRaw);

                    // ——— button label extraction
                    string? buttonText = null;
                    if (string.Equals(type, "button", StringComparison.OrdinalIgnoreCase))
                    {
                        buttonText = msg.TryGetProperty("button", out var btn) &&
                                     btn.TryGetProperty("text", out var textProp)
                                       ? textProp.GetString()?.Trim()
                                       : null;
                    }
                    else if (string.Equals(type, "interactive", StringComparison.OrdinalIgnoreCase) &&
                             msg.TryGetProperty("interactive", out var interactive))
                    {
                        if (interactive.TryGetProperty("type", out var intrType) &&
                            string.Equals(intrType.GetString(), "button_reply", StringComparison.OrdinalIgnoreCase) &&
                            interactive.TryGetProperty("button_reply", out var br) &&
                            br.TryGetProperty("title", out var titleProp))
                        {
                            buttonText = titleProp.GetString()?.Trim();
                        }
                        else if (interactive.TryGetProperty("list_reply", out var lr) &&
                                 lr.TryGetProperty("title", out var listTitleProp))
                        {
                            buttonText = listTitleProp.GetString()?.Trim();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(buttonText) || string.IsNullOrWhiteSpace(originalMessageId))
                    {
                        _logger.LogDebug("ℹ️ Not a recognized click or missing context.id. type={Type}", type);
                        continue;
                    }

                    _logger.LogInformation("🖱️ Button Click → From: {From}, ClickId: {ClickId}, OrigMsgId: {OrigId}, Text: {Text}",
                        fromDigits, clickMessageId, originalMessageId, buttonText);

                    // —— Try 1: originating MessageLog (for flow-sent messages)
                    var origin = await _context.MessageLogs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m =>
                            m.MessageId == originalMessageId &&
                            m.CTAFlowConfigId != null &&
                            m.CTAFlowStepId != null);

                    Guid businessId;
                    Guid flowId;
                    Guid stepId;
                    string? bundleJson = null;
                    int? flowVersion = null;

                    Guid? campaignSendLogId = null; // link the click to the shown message
                    Guid? runId = null;             // copy from parent CSL when available

                    if (origin != null)
                    {
                        businessId = origin.BusinessId;
                        flowId = origin.CTAFlowConfigId!.Value;
                        stepId = origin.CTAFlowStepId!.Value;
                        bundleJson = origin.ButtonBundleJson;
                        flowVersion = origin.FlowVersion;

                        // Map back to CSL via MessageLogId or WAMID and fetch RunId
                        var cslInfo = await _context.CampaignSendLogs
                            .AsNoTracking()
                            .Where(csl => (csl.MessageLogId == origin.Id) || (csl.MessageId == originalMessageId))
                            .OrderByDescending(csl => csl.CreatedAt)
                            .Select(csl => new { csl.Id, csl.RunId })
                            .FirstOrDefaultAsync();

                        campaignSendLogId = cslInfo?.Id;
                        runId = cslInfo?.RunId;
                    }
                    else
                    {
                        // —— Try 2: first campaign message (CampaignSendLogs)
                        var sendLog = await _context.CampaignSendLogs
                            .Include(sl => sl.Campaign)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(sl => sl.MessageId == originalMessageId);

                        if (sendLog == null)
                        {
                            _logger.LogWarning("❌ No MessageLog or CampaignSendLog for original WAMID {Orig}", originalMessageId);
                            continue;
                        }

                        businessId = sendLog.BusinessId != Guid.Empty
                            ? sendLog.BusinessId
                            : (sendLog.Campaign?.BusinessId ?? Guid.Empty);

                        if (businessId == Guid.Empty)
                        {
                            _logger.LogWarning("❌ Could not resolve BusinessId for WAMID {Orig}", originalMessageId);
                            continue;
                        }

                        campaignSendLogId = sendLog.Id;
                        runId = sendLog.RunId;

                        if (sendLog.CTAFlowConfigId.HasValue && sendLog.CTAFlowStepId.HasValue)
                        {
                            flowId = sendLog.CTAFlowConfigId.Value;
                            stepId = sendLog.CTAFlowStepId.Value;
                        }
                        else if (sendLog.Campaign?.CTAFlowConfigId != null)
                        {
                            flowId = sendLog.Campaign.CTAFlowConfigId.Value;

                            var entry = await _context.CTAFlowSteps
                                .Where(s => s.CTAFlowConfigId == flowId)
                                .OrderBy(s => s.StepOrder)
                                .Select(s => s.Id)
                                .FirstOrDefaultAsync();

                            if (entry == Guid.Empty)
                            {
                                _logger.LogWarning("❌ No entry step found for flow {Flow}", flowId);
                                continue;
                            }

                            stepId = entry;
                        }
                        else
                        {
                            _logger.LogWarning("❌ No flow context on CampaignSendLog for WAMID {Orig}", originalMessageId);
                            continue;
                        }

                        bundleJson = sendLog.ButtonBundleJson;
                    }

                    // ─────────────────────────────────────────────────────────────
                    // ✅ UPSERT PROFILE NAME (create-or-update) *before* next step
                    //    ensure we look up by digits-only phone.
                    // ─────────────────────────────────────────────────────────────
                    try
                    {
                        var profileName = TryGetProfileName(value);
                        if (!string.IsNullOrWhiteSpace(profileName))
                        {
                            var now = DateTime.UtcNow;
                            var contact = await _context.Contacts
                                .FirstOrDefaultAsync(c => c.BusinessId == businessId &&
                                                          (c.PhoneNumber == fromDigits || c.PhoneNumber == fromRaw));

                            if (contact == null)
                            {
                                profileName = profileName ?? "User";
                                contact = new Contact
                                {
                                    Id = Guid.NewGuid(),
                                    BusinessId = businessId,
                                    PhoneNumber = fromDigits, // store canonical
                                    Name = profileName,
                                    ProfileName = profileName,
                                    ProfileNameUpdatedAt = now,
                                    CreatedAt = now,
                                };
                                _context.Contacts.Add(contact);
                                await _context.SaveChangesAsync();
                                _logger.LogInformation("👤 Created contact + stored WA profile '{Name}' for {Phone} (biz {Biz})",
                                    profileName, fromDigits, businessId);
                            }
                            else
                            {
                                var changed = false;

                                if (!string.Equals(contact.ProfileName, profileName, StringComparison.Ordinal))
                                {
                                    contact.ProfileName = profileName;
                                    contact.ProfileNameUpdatedAt = now;
                                    changed = true;
                                }

                                if (string.IsNullOrWhiteSpace(contact.Name) ||
                                    contact.Name == "WhatsApp User" ||
                                    contact.Name == contact.PhoneNumber)
                                {
                                    if (!string.Equals(contact.Name, profileName, StringComparison.Ordinal))
                                    {
                                        contact.Name = profileName;
                                        changed = true;
                                    }
                                }

                                if (changed)
                                {
                                    contact.ProfileNameUpdatedAt = now;
                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation("👤 Updated WA profile name to '{Name}' for {Phone} (biz {Biz})",
                                        profileName, fromDigits, businessId);
                                }
                            }
                        }
                    }
                    catch (Exception exProf)
                    {
                        _logger.LogWarning(exProf, "⚠️ Failed to upsert WA profile name on click webhook.");
                    }

                    // —— Map clicked text -> button index via the shown bundle
                    short? buttonIndex = null;
                    FlowBtnBundleNode? hit = null;

                    if (!string.IsNullOrWhiteSpace(bundleJson))
                    {
                        try
                        {
                            var nodes = System.Text.Json.JsonSerializer
                                .Deserialize<List<FlowBtnBundleNode>>(bundleJson) ?? new();

                            hit = nodes.FirstOrDefault(n =>
                                      string.Equals(n.t ?? "", buttonText, StringComparison.OrdinalIgnoreCase))
                                  ?? nodes.FirstOrDefault(n => Norm(n.t) == Norm(buttonText));

                            if (hit != null)
                                buttonIndex = (short)hit.i;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Failed to parse ButtonBundleJson");
                        }
                    }

                    // —— Fallback: find link by TEXT for this step
                    FlowButtonLink? linkMatchedByText = null;
                    if (buttonIndex == null)
                    {
                        var stepLinks = await _context.FlowButtonLinks
                            .Where(l => l.CTAFlowStepId == stepId)
                            .OrderBy(l => l.ButtonIndex)
                            .ToListAsync();

                        if (stepLinks.Count > 0)
                        {
                            linkMatchedByText = stepLinks.FirstOrDefault(l =>
                                string.Equals(l.ButtonText ?? "", buttonText, StringComparison.OrdinalIgnoreCase))
                                ?? stepLinks.FirstOrDefault(l => Norm(l.ButtonText) == Norm(buttonText));

                            if (linkMatchedByText == null && stepLinks.Count == 1)
                            {
                                linkMatchedByText = stepLinks[0];
                                _logger.LogInformation("🟨 Falling back to single available link for step {Step}", stepId);
                            }

                            if (linkMatchedByText != null)
                            {
                                buttonIndex = (short?)linkMatchedByText.ButtonIndex;
                                _logger.LogInformation("✅ Mapped click by TEXT to index {Idx} (flow={Flow}, step={Step})",
                                    buttonIndex, flowId, stepId);
                            }
                        }
                    }

                    if (buttonIndex == null)
                    {
                        _logger.LogInformation("🟡 Button text not found in bundle or flow links. Ref={Ref}, Text='{Text}'",
                            originalMessageId, buttonText);
                        continue;
                    }

                    // —— Prefer exact link by index; otherwise use the text-matched link
                    var link = await _flowService.GetLinkAsync(flowId, stepId, buttonIndex.Value)
                               ?? linkMatchedByText;

                    if (link == null)
                    {
                        _logger.LogInformation("🟡 No button link for (flow={Flow}, step={Step}, idx={Idx})",
                            flowId, stepId, buttonIndex);
                        continue;
                    }

                    // —— Resolve index + step name (for logging)
                    short resolvedIndex = buttonIndex ?? Convert.ToInt16(link.ButtonIndex);
                    var stepName = await _context.CTAFlowSteps
                        .Where(s => s.Id == stepId)
                        .Select(s => s.TemplateToSend)
                        .FirstOrDefaultAsync() ?? string.Empty;

                    // ————————————————
                    // 📝 WRITE CLICK LOG (always, even if terminal)
                    // ————————————————
                    try
                    {
                        var clickExec = new FlowExecutionLog
                        {
                            Id = Guid.NewGuid(),
                            BusinessId = businessId,
                            FlowId = flowId,
                            StepId = stepId,
                            StepName = stepName,
                            CampaignSendLogId = campaignSendLogId,
                            MessageLogId = origin?.Id,
                            ContactPhone = fromDigits,      // digits-only
                            ButtonIndex = resolvedIndex,
                            TriggeredByButton = buttonText,
                            TemplateName = null,
                            TemplateType = "quick_reply",
                            Success = true,
                            ExecutedAt = DateTime.UtcNow,
                            RequestId = Guid.NewGuid(),
                            RunId = runId
                        };

                        _context.FlowExecutionLogs.Add(clickExec);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception exSave)
                    {
                        _logger.LogWarning(exSave, "⚠️ Failed to persist FlowExecutionLog (click). Continuing…");
                    }

                    // ===== RUNNING CTA JOURNEY STATE UPSERT (ONLY IF THIS BIZ IS CONFIGURED) =====
                    string runningJourney;

                    // Check once if this business is configured to receive CTAJourney.
                    bool shouldTrackState = await _context.CustomerWebhookConfigs
                        .AsNoTracking()
                        .AnyAsync(x => x.BusinessId == businessId && x.IsActive);

                    if (shouldTrackState)
                    {
                        try
                        {
                            // load current state for (business, flow, phone)
                            var state = await _context.ContactJourneyStates
                                .SingleOrDefaultAsync(s =>
                                    s.BusinessId == businessId &&
                                    s.FlowId == flowId &&
                                    s.ContactPhone == fromDigits);

                            if (state == null)
                            {
                                // first click -> start with this button text (original casing)
                                state = new ContactJourneyState
                                {
                                    Id = Guid.NewGuid(),
                                    BusinessId = businessId,
                                    FlowId = flowId,
                                    ContactPhone = fromDigits,
                                    JourneyText = buttonText ?? string.Empty,
                                    ClickCount = 1,
                                    LastButtonText = buttonText,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                _context.ContactJourneyStates.Add(state);
                                await _context.SaveChangesAsync();
                                runningJourney = state.JourneyText;
                                _logger.LogInformation("🧵 Journey init: {Journey} (biz={Biz}, flow={Flow}, phone={Phone})",
                                    runningJourney, businessId, flowId, fromDigits);
                            }
                            else
                            {
                                // append EVERY press (duplicates allowed), keep original casing
                                var parts = (state.JourneyText ?? string.Empty)
                                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .ToList();

                                if (!string.IsNullOrWhiteSpace(buttonText))
                                    parts.Add(buttonText!);

                                // cap growth
                                const int cap = 15;
                                if (parts.Count > cap) parts = parts.Skip(parts.Count - cap).ToList();

                                state.JourneyText = string.Join('/', parts);
                                state.ClickCount += 1;
                                state.LastButtonText = buttonText;
                                state.UpdatedAt = DateTime.UtcNow;

                                await _context.SaveChangesAsync();
                                runningJourney = state.JourneyText ?? string.Empty;

                                _logger.LogInformation("🧵 Journey update: {Journey} (biz={Biz}, flow={Flow}, phone={Phone})",
                                    runningJourney, businessId, flowId, fromDigits);
                            }
                        }
                        catch (Exception exState)
                        {
                            _logger.LogWarning(exState, "⚠️ Failed to upsert ContactJourneyState.");
                            // fall back to this click only
                            runningJourney = buttonText ?? string.Empty;
                        }
                    }
                    else
                    {
                        // Business not configured → do NOT save any state. Just use the current button for emit.
                        runningJourney = buttonText ?? string.Empty;
                    }
                    // ===== END RUNNING CTA JOURNEY STATE UPSERT =====

                    // ===== CTAJourney EMIT (running journey) =====
                    try
                    {
                        // contact (for userName / userPhone)
                        var contact = await _context.Contacts
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.PhoneNumber == fromDigits);

                        // Prefer sender from the originating send (keeps the same WABA)
                        string? senderProvider = null;
                        string? senderPhoneNumberId = null;

                        if (campaignSendLogId.HasValue)
                        {
                            var originSend = await _context.CampaignSendLogs
                                .AsNoTracking()
                                .Include(s => s.Campaign)
                                .Where(s => s.Id == campaignSendLogId.Value)
                                .Select(s => new { s.Campaign.Provider, s.Campaign.PhoneNumberId })
                                .FirstOrDefaultAsync();

                            senderProvider = originSend?.Provider;
                            senderPhoneNumberId = originSend?.PhoneNumberId;
                        }
                        if (string.IsNullOrWhiteSpace(senderPhoneNumberId) && origin?.CampaignId != null)
                        {
                            var originCamp = await _context.Campaigns
                                .AsNoTracking()
                                .Where(c => c.Id == origin.CampaignId.Value)
                                .Select(c => new { c.Provider, c.PhoneNumberId })
                                .FirstOrDefaultAsync();

                            senderProvider ??= originCamp?.Provider;
                            senderPhoneNumberId = originCamp?.PhoneNumberId;
                        }

                        // Map PhoneNumberId -> WhatsAppBusinessNumber, or choose a default for the same provider
                        string? waBusinessNumber = null;

                        if (!string.IsNullOrWhiteSpace(senderPhoneNumberId))
                        {
                            waBusinessNumber = await _context.WhatsAppPhoneNumbers
                                .AsNoTracking()
                                .Where(n => n.BusinessId == businessId && n.PhoneNumberId == senderPhoneNumberId)
                                .Select(n => n.WhatsAppBusinessNumber)
                                .FirstOrDefaultAsync();
                        }

                        if (string.IsNullOrWhiteSpace(waBusinessNumber))
                        {
                            var row = await _context.WhatsAppPhoneNumbers
                                .AsNoTracking()
                                .Where(n => n.BusinessId == businessId &&
                                            n.IsActive &&
                                            (string.IsNullOrEmpty(senderProvider) || n.Provider == senderProvider))
                                .OrderByDescending(n => n.IsDefault)
                                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                                .Select(n => new { n.PhoneNumberId, n.WhatsAppBusinessNumber })
                                .FirstOrDefaultAsync();

                            senderPhoneNumberId ??= row?.PhoneNumberId;
                            waBusinessNumber = row?.WhatsAppBusinessNumber;
                        }

                        var dto = CtaJourneyMapper.Build(
                            journeyKey: runningJourney,                      // running state with original casing
                            contact: contact,
                            profileName: contact?.ProfileName ?? contact?.Name,
                            userId: null,
                            phoneNumberId: waBusinessNumber,                 // publish WA display number as bot id
                            businessDisplayPhone: waBusinessNumber,          // same as above
                            categoryBrowsed: null,
                            productBrowsed: null
                        );

                        await _journeyPublisher.PublishAsync(businessId, dto, CancellationToken.None);
                        _logger.LogInformation("📤 CTAJourney posted (running): {Journey} (biz={Biz}, phone={Phone})",
                            dto.CTAJourney, businessId, dto.userPhone);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Failed to post CTAJourney (click). Continuing…");
                    }
                    // ===== end CTAJourney EMIT =====

                    // —— If terminal/URL button: already logged the click
                    if (link.NextStepId == null)
                    {
                        _logger.LogInformation("🔚 Terminal/URL button: no NextStepId. flow={Flow}, step={Step}, idx={Idx}, text='{Text}'",
                            flowId, stepId, resolvedIndex, link.ButtonText);
                        continue;
                    }

                    if (_flowRuntime == null)
                    {
                        _logger.LogError("❌ _flowRuntime is null. Cannot execute next step. flow={Flow}, step={Step}, idx={Idx}", flowId, stepId, resolvedIndex);
                        continue;
                    }

                    // —— 🔎 Resolve sender from the originating campaign/send (use SAME WABA) for next step
                    string? providerFromCampaign = null;
                    string? phoneNumberIdFromCampaign = null;

                    if (campaignSendLogId.HasValue)
                    {
                        var originSend = await _context.CampaignSendLogs
                            .AsNoTracking()
                            .Include(s => s.Campaign)
                            .Where(s => s.Id == campaignSendLogId.Value)
                            .Select(s => new
                            {
                                s.Campaign.Provider,
                                s.Campaign.PhoneNumberId
                            })
                            .FirstOrDefaultAsync();

                        providerFromCampaign = originSend?.Provider;
                        phoneNumberIdFromCampaign = originSend?.PhoneNumberId;
                    }
                    else if (origin != null && origin.CampaignId.HasValue)
                    {
                        var originCamp = await _context.Campaigns
                            .AsNoTracking()
                            .Where(c => c.Id == origin.CampaignId.Value)
                            .Select(c => new { c.Provider, c.PhoneNumberId })
                            .FirstOrDefaultAsync();

                        providerFromCampaign = originCamp?.Provider;
                        phoneNumberIdFromCampaign = originCamp?.PhoneNumberId;
                    }

                    // —— Execute next (carry sender forward)
                    var ctxObj = new NextStepContext
                    {
                        BusinessId = businessId,
                        FlowId = flowId,
                        Version = flowVersion ?? 1,
                        SourceStepId = stepId,
                        TargetStepId = link.NextStepId!.Value,
                        ButtonIndex = resolvedIndex,
                        MessageLogId = origin?.Id ?? Guid.Empty,
                        ContactPhone = fromDigits,     // digits-only
                        RequestId = Guid.NewGuid(),
                        ClickedButton = link,

                        // carry same sender into the next step
                        Provider = providerFromCampaign,
                        PhoneNumberId = phoneNumberIdFromCampaign,
                        AlwaysSend = true // force runtime to send even if it’s a loopback/same step
                    };

                    try
                    {
                        var result = await _flowRuntime.ExecuteNextAsync(ctxObj);

                        if (result.Success && !string.IsNullOrWhiteSpace(result.RedirectUrl))
                        {
                            _logger.LogInformation("🔗 URL button redirect (logical): {Url}", result.RedirectUrl);
                        }
                    }
                    catch (Exception exRun)
                    {
                        _logger.LogError(exRun,
                            "❌ ExecuteNextAsync failed. ctx: flow={Flow} step={Step} next={Next} idx={Idx} from={From} orig={Orig} text='{Text}'",
                            ctxObj.FlowId, ctxObj.SourceStepId, ctxObj.TargetStepId, ctxObj.ButtonIndex, fromDigits, originalMessageId, buttonText);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process CTA button click.");
            }
        }

        private sealed class FlowBtnBundleNode
        {
            public int i { get; init; }
            public string? t { get; init; }   // button text/title
            public string? ty { get; init; }  // button type (URL/QUICK_REPLY/FLOW)
            public string? v { get; init; }   // value/payload (e.g., URL)
            public Guid? ns { get; init; }    // next step id
        }
        private static string ToKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unknown";
            // letters/digits → lower, spaces/._- → underscore, strip the rest
            var chars = s.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();
            var key = new string(chars);
            // squeeze duplicate underscores
            while (key.Contains("__")) key = key.Replace("__", "_");
            return key.Trim('_');
        }

    }
}
