using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.AutoReplyBuilder.Services;
using xbytechat.api.Features.Automation.Services;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.Inbox.Hubs;
using xbytechat.api.Features.Inbox.Services;
using xbytechat.api.Features.Webhooks.Directory;
using xbytechat.api.Features.CRM.Services;

namespace xbytechat.api.Features.Webhooks.Services.Processors
{
    public class InboundMessageProcessor : IInboundMessageProcessor
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<InboxHub> _hubContext;
        private readonly ILogger<InboundMessageProcessor> _logger;
        private readonly IInboxService _inboxService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHubContext<InboxHub> _hub;
        private readonly IContactProfileService _contactProfile;
        private readonly IProviderDirectory _providerDirectory;

        public InboundMessageProcessor(
            AppDbContext context,
            IHubContext<InboxHub> hubContext,
            ILogger<InboundMessageProcessor> logger,
            IInboxService inboxService,
            IServiceScopeFactory serviceScopeFactory,
            IHubContext<InboxHub> hub,
            IContactProfileService contactProfile,
            IProviderDirectory providerDirectory
        )
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _inboxService = inboxService;
            _serviceScopeFactory = serviceScopeFactory;
            _hub = hub;
            _contactProfile = contactProfile;
            _providerDirectory = providerDirectory;
        }

        public async Task ProcessChatAsync(JsonElement value, JsonElement msg)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();
                var chatSessionStateService = scope.ServiceProvider.GetRequiredService<IChatSessionStateService>();
                var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
                var autoReplyRuntime = scope.ServiceProvider.GetRequiredService<IAutoReplyRuntimeService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<InboundMessageProcessor>>();
                var contactProfileService = scope.ServiceProvider.GetRequiredService<IContactProfileService>();
                var inboxService = scope.ServiceProvider.GetRequiredService<IInboxService>();

                // digits-only normalizer (matches how we store/search phones)
                static string Normalize(string? s) =>
                    string.IsNullOrWhiteSpace(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

                var msgType = msg.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString()
                    : "unknown";

                var rawContactPhone = msg.GetProperty("from").GetString() ?? "";
                var contactPhone = Normalize(rawContactPhone);

                // ✅ WAMID / Provider message id (used for idempotency)
                var wamid = msg.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                wamid = string.IsNullOrWhiteSpace(wamid) ? null : wamid.Trim();

                // ✅ Provider timestamp (unix seconds) → use for SentAt ordering consistency
                DateTime providerSentAtUtc = DateTime.UtcNow;
                if (msg.TryGetProperty("timestamp", out var tsProp))
                {
                    long unixSeconds = 0;

                    if (tsProp.ValueKind == JsonValueKind.String)
                    {
                        var tsStr = tsProp.GetString();
                        if (!string.IsNullOrWhiteSpace(tsStr))
                            long.TryParse(tsStr, out unixSeconds);
                    }
                    else if (tsProp.ValueKind == JsonValueKind.Number)
                    {
                        tsProp.TryGetInt64(out unixSeconds);
                    }

                    if (unixSeconds > 0)
                        providerSentAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                }

                string? content = msgType switch
                {
                    "text" when msg.TryGetProperty("text", out var t) &&
                                t.TryGetProperty("body", out var b)
                                => b.GetString(),

                    "image" when msg.TryGetProperty("image", out var img) &&
                                 img.TryGetProperty("caption", out var cap)
                                 => cap.GetString(),

                    "document" when msg.TryGetProperty("document", out var doc) &&
                                    doc.TryGetProperty("caption", out var dcap)
                                    => dcap.GetString(),

                    "video" when msg.TryGetProperty("video", out var vid) &&
                                 vid.TryGetProperty("caption", out var vcap)
                                 => vcap.GetString(),

                    _ => null
                };

                string? inboundMediaId = null;
                string? inboundMediaType = null;
                string? inboundMimeType = null;
                string? inboundFileName = null;
                double? inboundLocationLat = null;
                double? inboundLocationLon = null;
                string? inboundLocationName = null;
                string? inboundLocationAddress = null;

                if (msgType == "image" && msg.TryGetProperty("image", out var imgObj) && imgObj.ValueKind == JsonValueKind.Object)
                {
                    inboundMediaType = "image";

                    if (imgObj.TryGetProperty("id", out var mid) && mid.ValueKind == JsonValueKind.String)
                        inboundMediaId = mid.GetString();

                    if (imgObj.TryGetProperty("mime_type", out var mt) && mt.ValueKind == JsonValueKind.String)
                        inboundMimeType = mt.GetString();
                }
                else if (msgType == "document" && msg.TryGetProperty("document", out var docObj) && docObj.ValueKind == JsonValueKind.Object)
                {
                    inboundMediaType = "document";

                    if (docObj.TryGetProperty("id", out var mid) && mid.ValueKind == JsonValueKind.String)
                        inboundMediaId = mid.GetString();

                    if (docObj.TryGetProperty("mime_type", out var mt) && mt.ValueKind == JsonValueKind.String)
                        inboundMimeType = mt.GetString();

                    if (docObj.TryGetProperty("filename", out var fn) && fn.ValueKind == JsonValueKind.String)
                        inboundFileName = fn.GetString();
                }
                else if (msgType == "video" && msg.TryGetProperty("video", out var vidObj) && vidObj.ValueKind == JsonValueKind.Object)
                {
                    inboundMediaType = "video";

                    if (vidObj.TryGetProperty("id", out var mid) && mid.ValueKind == JsonValueKind.String)
                        inboundMediaId = mid.GetString();

                    if (vidObj.TryGetProperty("mime_type", out var mt) && mt.ValueKind == JsonValueKind.String)
                        inboundMimeType = mt.GetString();
                }
                else if (msgType == "audio" && msg.TryGetProperty("audio", out var audObj) && audObj.ValueKind == JsonValueKind.Object)
                {
                    inboundMediaType = "audio";

                    if (audObj.TryGetProperty("id", out var mid) && mid.ValueKind == JsonValueKind.String)
                        inboundMediaId = mid.GetString();

                    if (audObj.TryGetProperty("mime_type", out var mt) && mt.ValueKind == JsonValueKind.String)
                        inboundMimeType = mt.GetString();
                }
                else if (msgType == "location" && msg.TryGetProperty("location", out var locObj) && locObj.ValueKind == JsonValueKind.Object)
                {
                    inboundMediaType = "location";

                    if (locObj.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number && lat.TryGetDouble(out var v1))
                        inboundLocationLat = v1;

                    if (locObj.TryGetProperty("longitude", out var lon) && lon.ValueKind == JsonValueKind.Number && lon.TryGetDouble(out var v2))
                        inboundLocationLon = v2;

                    if (locObj.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                        inboundLocationName = nm.GetString();

                    if (locObj.TryGetProperty("address", out var addr) && addr.ValueKind == JsonValueKind.String)
                        inboundLocationAddress = addr.GetString();

                    if (string.IsNullOrWhiteSpace(content))
                        content = inboundLocationName ?? inboundLocationAddress ?? "Location";
                }

                logger.LogInformation(
                    "📥 Inbound WA message: type={MsgType}, from={From}, wamid={Wamid}, providerTsUtc={ProviderTsUtc}, preview={Preview}",
                    msgType,
                    rawContactPhone,
                    wamid,
                    providerSentAtUtc,
                    content?.Length > 50 ? content[..50] : content
                );

                // 2) Resolve business via ProviderDirectory first, then fallback to WhatsAppPhoneNumbers
                if (!value.TryGetProperty("metadata", out var metadata))
                {
                    logger.LogWarning("Inbound: metadata missing on webhook payload.");
                    return;
                }

                string? displayNumber = metadata.TryGetProperty("display_phone_number", out var dn)
                    ? dn.GetString()
                    : null;

                string? phoneNumberId = metadata.TryGetProperty("phone_number_id", out var pn)
                    ? pn.GetString()
                    : null;

                string? wabaId = metadata.TryGetProperty("waba_id", out var we)
                    ? we.GetString()
                    : null;

                // 2.1 Prefer provider directory
                Guid? businessId = await _providerDirectory.ResolveBusinessIdAsync(
                    provider: "meta_cloud",
                    phoneNumberId: phoneNumberId,
                    displayPhoneNumber: displayNumber,
                    wabaId: wabaId,
                    waId: rawContactPhone
                );

                // 2.2 Fallback to legacy WhatsAppPhoneNumbers by display number if needed
                if (businessId == null && !string.IsNullOrWhiteSpace(displayNumber))
                {
                    var cleanIncomingBiz = Normalize(displayNumber);

                    var candidates = await db.WhatsAppPhoneNumbers
                        .AsNoTracking()
                        .Where(n => n.IsActive)
                        .Select(n => new { n.BusinessId, n.WhatsAppBusinessNumber })
                        .ToListAsync();

                    var numHit = candidates.FirstOrDefault(n =>
                        Normalize(n.WhatsAppBusinessNumber) == cleanIncomingBiz);

                    if (numHit != null)
                        businessId = numHit.BusinessId;
                }

                if (businessId == null || businessId == Guid.Empty)
                {
                    logger.LogWarning(
                        "❌ Inbound: business not resolved. phone_number_id={PhoneId}, display={Display}, waba={Waba}, from={From}, wamid={Wamid}",
                        phoneNumberId,
                        displayNumber,
                        wabaId,
                        rawContactPhone,
                        wamid
                    );
                    return;
                }

                var resolvedBusinessId = businessId.Value;

                // 3) Find or create contact
                var contact = await contactService.FindOrCreateAsync(resolvedBusinessId, contactPhone);
                if (contact == null)
                {
                    logger.LogWarning("❌ Could not resolve contact for phone: {Phone}", contactPhone);
                    return;
                }

                static string? TryGetProfileName(JsonElement root)
                {
                    if (root.TryGetProperty("contacts", out var contactsEl) &&
                        contactsEl.ValueKind == JsonValueKind.Array &&
                        contactsEl.GetArrayLength() > 0)
                    {
                        var c0 = contactsEl[0];
                        if (c0.TryGetProperty("profile", out var prof) &&
                            prof.ValueKind == JsonValueKind.Object &&
                            prof.TryGetProperty("name", out var nm) &&
                            nm.ValueKind == JsonValueKind.String)
                        {
                            var n = nm.GetString();
                            return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
                        }
                    }
                    return null;
                }

                var profileName = TryGetProfileName(value);
                if (!string.IsNullOrWhiteSpace(profileName))
                {
                    try
                    {
                        await contactProfileService.UpsertProfileNameAsync(
                            resolvedBusinessId,
                            contactPhone,
                            profileName!,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "⚠️ Failed to upsert ProfileName for {Phone}", contactPhone);
                    }
                }

                // 4) Read chat mode (still fine to read)
                var mode = await chatSessionStateService.GetChatModeAsync(resolvedBusinessId, contact.Id);
                var isAgentMode = mode == "agent";

                // 5) Update conversation timestamps + auto-reopen on inbound (best-effort)
                try
                {
                    contact.LastInboundAt = providerSentAtUtc;

                    var inboxStatus = (contact.InboxStatus ?? string.Empty).Trim();
                    if (string.Equals(inboxStatus, "Closed", StringComparison.OrdinalIgnoreCase) ||
                        contact.IsArchived ||
                        !contact.IsActive)
                    {
                        contact.InboxStatus = "Open";
                        contact.IsArchived = false;
                        contact.IsActive = true;
                    }

                    // ✅ persist contact updates (best effort)
                    await db.SaveChangesAsync();
                }
                catch { /* best-effort */ }

                // ✅ Single-writer rule:
                // Always persist inbound via InboxService. No direct db.MessageLogs.Add here.
                var saved = await inboxService.SaveIncomingMessageAsync(new InboxMessageDto
                {
                    BusinessId = resolvedBusinessId,
                    ContactId = contact.Id,

                    // NOTE: your current storage convention uses RecipientPhone as the "other party" number
                    RecipientPhone = contactPhone,

                    MessageBody = content ?? string.Empty,
                    MediaId = inboundMediaId,
                    MediaType = inboundMediaType,
                    FileName = inboundFileName,
                    MimeType = inboundMimeType,
                    LocationLatitude = inboundLocationLat,
                    LocationLongitude = inboundLocationLon,
                    LocationName = inboundLocationName,
                    LocationAddress = inboundLocationAddress,
                    IsIncoming = true,
                    Status = "received",
                    SentAt = providerSentAtUtc,

                    // ✅ Provider id (Meta WAMID) for idempotency
                    ProviderMessageId = wamid
                });

                // Push SignalR event (prefer SentAt if available for correct thread ordering)
                var sentAtForUi = saved.SentAt ?? saved.CreatedAt;

                // ✅ IMPORTANT: align payload keys with InboxHub.SendMessageToContact:
                // use "messageContent" (not "message")
                await _hub.Clients
                    .Group($"business_{resolvedBusinessId}")
                    .SendAsync("ReceiveInboxMessage", new
                    {
                        contactId = contact.Id,
                        messageContent = saved.MessageContent,
                        isIncoming = true,
                        senderId = (Guid?)null,
                        status = saved.Status,
                        sentAt = sentAtForUi,

                        // ✅ helps UI match / debug
                        logId = saved.Id,
                        messageLogId = saved.Id,
                        providerMessageId = saved.ProviderMessageId,

                        // ✅ media support (image/pdf)
                        mediaId = saved.MediaId,
                        mediaType = saved.MediaType,
                        fileName = saved.FileName,
                        mimeType = saved.MimeType,
                        locationLatitude = saved.LocationLatitude,
                        locationLongitude = saved.LocationLongitude,
                        locationName = saved.LocationName,
                        locationAddress = saved.LocationAddress
                    });

                // 6) Try AutoReply runtime first, then fall back to legacy automation
                try
                {
                    var triggerRaw = (content ?? string.Empty).Trim();
                    var triggerKeyword = triggerRaw.ToLowerInvariant();

                    var autoHandled = false;

                    if (!string.IsNullOrWhiteSpace(triggerRaw))
                    {
                        var autoResult = await autoReplyRuntime.TryHandleAsync(
                            resolvedBusinessId,
                            contact.Id,
                            contact.PhoneNumber,
                            triggerRaw,
                            CancellationToken.None
                        );

                        autoHandled = autoResult.Handled;

                        if (autoResult.Handled)
                        {
                            logger.LogInformation(
                                "🤖 AutoReply runtime handled inbound message. BusinessId={BusinessId}, ContactId={ContactId}, Keyword={Keyword}, SentSimpleReply={SentSimpleReply}, StartedCtaFlow={StartedCtaFlow}, AutoReplyFlowId={FlowId}, CtaFlowConfigId={CtaId}",
                                resolvedBusinessId,
                                contact.Id,
                                triggerKeyword,
                                autoResult.SentSimpleReply,
                                autoResult.StartedCtaFlow,
                                autoResult.AutoReplyFlowId,
                                autoResult.CtaFlowConfigId
                            );
                        }
                        else
                        {
                            logger.LogInformation(
                                "🤖 AutoReply runtime did not handle message. Falling back to legacy automation. Keyword={Keyword}",
                                triggerKeyword
                            );
                        }
                    }

                    if (!autoHandled)
                    {
                        var handledByLegacy = await automationService.TryRunFlowByKeywordAsync(
                            resolvedBusinessId,
                            triggerKeyword,
                            contact.PhoneNumber,
                            sourceChannel: "whatsapp",
                            industryTag: "default");

                        if (!handledByLegacy)
                        {
                            logger.LogInformation("🕵️ No automation flow matched keyword (legacy): {Keyword}", triggerKeyword);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ AutoReply / Automation flow execution failed.");
                }

                logger.LogInformation(
                    "✅ Inbound persisted via InboxService (single-writer). mode={Mode}, businessId={BusinessId}, contactId={ContactId}, wamid={Wamid}",
                    isAgentMode ? "agent" : "bot",
                    resolvedBusinessId,
                    contact.Id,
                    wamid
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process inbound WhatsApp chat.");
            }
        }

        public async Task ProcessInteractiveAsync(JsonElement value, CancellationToken ct = default)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var contactProfileService = scope.ServiceProvider.GetRequiredService<IContactProfileService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InboundMessageProcessor>>();

            static string Normalize(string? number) =>
                string.IsNullOrWhiteSpace(number) ? "" : new string(number.Where(char.IsDigit).ToArray());

            static string? TryGetProfileName(JsonElement root)
            {
                if (root.TryGetProperty("contacts", out var contactsEl) &&
                    contactsEl.ValueKind == JsonValueKind.Array &&
                    contactsEl.GetArrayLength() > 0)
                {
                    var c0 = contactsEl[0];
                    if (c0.TryGetProperty("profile", out var profileEl) &&
                        profileEl.ValueKind == JsonValueKind.Object &&
                        profileEl.TryGetProperty("name", out var nameEl) &&
                        nameEl.ValueKind == JsonValueKind.String)
                    {
                        var n = nameEl.GetString();
                        return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
                    }
                }
                return null;
            }

            if (!value.TryGetProperty("messages", out var msgs) || msgs.ValueKind != JsonValueKind.Array || msgs.GetArrayLength() == 0)
                return;

            var msg0 = msgs[0];
            var fromRaw = msg0.GetProperty("from").GetString() ?? "";
            var fromE164 = Normalize(fromRaw);

            var displayNumberRaw = value.GetProperty("metadata").GetProperty("display_phone_number").GetString() ?? "";
            var displayNumber = Normalize(displayNumberRaw);

            var candidates = await db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(n => n.IsActive)
                .Select(n => new { n.BusinessId, n.WhatsAppBusinessNumber })
                .ToListAsync(ct);

            var numHit = candidates.FirstOrDefault(n => Normalize(n.WhatsAppBusinessNumber) == displayNumber);
            if (numHit == null)
            {
                logger.LogWarning("❌ Business not found for interactive webhook number: {Num}", displayNumberRaw);
                return;
            }

            var businessId = numHit.BusinessId;

            var profileName = TryGetProfileName(value);
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                try
                {
                    await contactProfileService.UpsertProfileNameAsync(businessId, fromE164, profileName!, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "⚠️ Failed to upsert ProfileName on interactive webhook for {Phone}", fromE164);
                }
            }

            // … continue your existing interactive handling
        }
    }
}
