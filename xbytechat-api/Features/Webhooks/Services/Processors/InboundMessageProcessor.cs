using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.Inbox.Hubs;
using Microsoft.Extensions.DependencyInjection;
using xbytechat.api.Features.AutoReplyBuilder.Services;
using xbytechat.api.Features.Inbox.Services;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.CRM.Services;
using xbytechat.api.Features.Automation.Services;
using xbytechat.api.Features.Webhooks.Directory;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Services; // 🔹 NEW: provider directory

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
        private readonly IProviderDirectory _providerDirectory; // 🔹 NEW

        public InboundMessageProcessor(
            AppDbContext context,
            IHubContext<InboxHub> hubContext,
            ILogger<InboundMessageProcessor> logger,
            IInboxService inboxService,
            IServiceScopeFactory serviceScopeFactory,
            IHubContext<InboxHub> hub,
            IContactProfileService contactProfile,
            IProviderDirectory providerDirectory // 🔹 NEW
        )
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _inboxService = inboxService;
            _serviceScopeFactory = serviceScopeFactory;
            _hub = hub;
            _contactProfile = contactProfile;
            _providerDirectory = providerDirectory; // 🔹 NEW
        }

        public async Task ProcessChatAsync(JsonElement value)
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

                // digits-only normalizer (matches how we store/search phones)
                static string Normalize(string? s) =>
                    string.IsNullOrWhiteSpace(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

                // 1) Extract WA metadata + message (Meta Cloud shape)
                if (!value.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0)
                {
                    logger.LogWarning("Inbound WA payload has no messages array.");
                    return;
                }

                var msg = messages[0];

                var msgType = msg.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString()
                    : "unknown";

                var rawContactPhone = msg.GetProperty("from").GetString() ?? "";
                var contactPhone = Normalize(rawContactPhone);

                string? content = msgType switch
                {
                    "text" when msg.TryGetProperty("text", out var t) &&
                                t.TryGetProperty("body", out var b)
                                => b.GetString(),

                    "image" when msg.TryGetProperty("image", out var img) &&
                                 img.TryGetProperty("caption", out var cap)
                                 => cap.GetString(),

                    _ => null
                };

                logger.LogInformation(
                    "📥 Inbound WA message: type={MsgType}, from={From}, preview={Preview}",
                    msgType,
                    rawContactPhone,
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

                // 2.1 Prefer provider directory (uses provider + phone_number_id + waba_id)
                Guid? businessId = await _providerDirectory.ResolveBusinessIdAsync(
                    provider: "meta_cloud",          // canonical provider key for Meta Cloud
                    phoneNumberId: phoneNumberId,
                    displayPhoneNumber: displayNumber,
                    wabaId: wabaId,
                    waId: rawContactPhone           // the WA user id ("from")
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

                // 2.3 Still nothing → log and bail
                if (businessId == null || businessId == Guid.Empty)
                {
                    logger.LogWarning(
                        "❌ Inbound: business not resolved. phone_number_id={PhoneId}, display={Display}, waba={Waba}, from={From}",
                        phoneNumberId,
                        displayNumber,
                        wabaId,
                        rawContactPhone
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

                // Extract profile name (contacts[0].profile.name) and upsert into Contacts
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

                // 4) Check chat mode…
                var mode = await chatSessionStateService.GetChatModeAsync(resolvedBusinessId, contact.Id);
                var isAgentMode = mode == "agent";

                // 5) Log incoming message
                var messageLog = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = resolvedBusinessId,
                    ContactId = contact.Id,
                    RecipientNumber = contactPhone,
                    MessageContent = content,
                    Status = "received",
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    IsIncoming = true
                };

                db.MessageLogs.Add(messageLog);
                await db.SaveChangesAsync();

                await _hub.Clients
                    .Group($"business_{resolvedBusinessId}")
                    .SendAsync("ReceiveInboxMessage", new
                    {
                        contactId = contact.Id,
                        message = messageLog.MessageContent,
                        isIncoming = true,
                        senderId = (Guid?)null,
                        sentAt = messageLog.CreatedAt
                    });

                // 6) Try AutoReply runtime first, then fall back to legacy automation
                try
                {
                    var triggerRaw = (content ?? string.Empty).Trim();
                    var triggerKeyword = triggerRaw.ToLowerInvariant();

                    var autoHandled = false;

                    // 6.1 – New AutoReply runtime (keyword → simple reply or CTA flow)
                    if (!string.IsNullOrWhiteSpace(triggerRaw))
                    {
                        var autoResult = await autoReplyRuntime.TryHandleAsync(
                            resolvedBusinessId,
                            contact.Id,
                            contact.PhoneNumber,
                            triggerRaw,                // pass original text (not forced lowercase)
                            CancellationToken.None     // you can thread a real ct later if you extend the method
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

                    // 6.2 – Legacy AutomationService fallback (only if AutoReply did NOT handle)
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


                // 7) Sync to inbox only if agent mode
                if (isAgentMode)
                {
                    try
                    {
                        var inboxService = scope.ServiceProvider.GetRequiredService<IInboxService>();

                        logger.LogInformation(
                            "📥 Inbound: syncing message to inbox for BusinessId={BusinessId}, ContactId={ContactId}",
                            resolvedBusinessId,
                            contact.Id);

                        await inboxService.SaveIncomingMessageAsync(new InboxMessageDto
                        {
                            BusinessId = resolvedBusinessId,
                            ContactId = contact.Id,
                            RecipientPhone = contact.PhoneNumber,
                            MessageBody = messageLog.MessageContent,
                            IsIncoming = true,
                            Status = messageLog.Status,
                            SentAt = messageLog.CreatedAt
                        });

                        logger.LogInformation("✅ Message synced to inbox for contact {Phone}", contactPhone);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "❌ Failed to sync inbound message to inbox.");
                    }
                }
                else
                {
                    logger.LogInformation("🚫 Skipping inbox sync: chat mode is not 'agent'");
                }
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

            // Safe extract of profile name (Meta Cloud shape)
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

            // messages[0].from is always present for interactive/button
            if (!value.TryGetProperty("messages", out var msgs) || msgs.GetArrayLength() == 0)
                return;

            var msg0 = msgs[0];
            var fromRaw = msg0.GetProperty("from").GetString() ?? "";
            var fromE164 = Normalize(fromRaw);

            // Resolve Business via metadata.display_phone_number → WhatsAppPhoneNumbers
            var displayNumberRaw = value.GetProperty("metadata").GetProperty("display_phone_number").GetString() ?? "";
            var displayNumber = Normalize(displayNumberRaw);

            // Look up the business by matching the normalized number in WhatsAppPhoneNumbers
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

            // Upsert profile name if present
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

            // … continue your existing interactive handling (routing to next step, etc.)
        }
    }
}


//using System;
//using System.Text.Json;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using xbytechat.api;
//using xbytechat.api.Features.Inbox.DTOs;
//using xbytechat.api.CRM.Models;
//using xbytechat.api.Features.Inbox.Hubs;
//using Microsoft.Extensions.DependencyInjection;
//using xbytechat.api.CRM.Interfaces;
//using xbytechat.api.Features.AutoReplyBuilder.Services;
//using xbytechat.api.Features.Inbox.Services;
//using xbytechat.api.Features.MessagesEngine.DTOs;
//using xbytechat.api.Features.MessagesEngine.Services;
//using xbytechat.api.CRM.Services;
//using xbytechat.api.Features.Automation.Services;
//using xbytechat.api.Features.Contacts.Services;

//namespace xbytechat.api.Features.Webhooks.Services.Processors
//{
//    public class InboundMessageProcessor : IInboundMessageProcessor
//    {
//        private readonly AppDbContext _context;
//        private readonly IHubContext<InboxHub> _hubContext;
//        private readonly ILogger<InboundMessageProcessor> _logger;
//        private readonly IInboxService _inboxService;
//        private readonly IServiceScopeFactory _serviceScopeFactory;
//        private readonly IHubContext<InboxHub> _hub;
//        private readonly IContactProfileService _contactProfile;

//        public InboundMessageProcessor(
//            AppDbContext context,
//            IHubContext<InboxHub> hubContext,
//            ILogger<InboundMessageProcessor> logger,
//            IInboxService inboxService,
//            IServiceScopeFactory serviceScopeFactory,
//            IHubContext<InboxHub> hub,
//            IContactProfileService contactProfile)
//        {
//            _context = context;
//            _hubContext = hubContext;
//            _logger = logger;
//            _inboxService = inboxService;
//            _serviceScopeFactory = serviceScopeFactory;
//            _hub = hub;
//            _contactProfile = contactProfile;
//        }

//        //public async Task ProcessChatAsync(JsonElement value)
//        //{
//        //    // High-level trace for every inbound chat
//        //    try
//        //    {
//        //        var rawText = value.GetRawText();
//        //        _logger.LogInformation(
//        //            "💬 InboundMessageProcessor.ProcessChatAsync started. PayloadLength={Length}",
//        //            rawText?.Length ?? 0);
//        //    }
//        //    catch
//        //    {
//        //        // ignore any GetRawText failures – not critical
//        //    }

//        //    try
//        //    {
//        //        using var scope = _serviceScopeFactory.CreateScope();
//        //        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//        //        var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();
//        //        var chatSessionStateService = scope.ServiceProvider.GetRequiredService<IChatSessionStateService>();
//        //        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
//        //        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InboundMessageProcessor>>();
//        //        var contactProfileService = scope.ServiceProvider.GetRequiredService<IContactProfileService>();

//        //        // digits-only normalizer (matches how we store/search phones)
//        //        static string Normalize(string? s) =>
//        //            string.IsNullOrWhiteSpace(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

//        //        // 1) Extract WA metadata + message (Meta Cloud shape)
//        //        if (!value.TryGetProperty("messages", out var msgs) || msgs.GetArrayLength() == 0)
//        //        {
//        //            logger.LogWarning("❌ Inbound payload has no 'messages' array or it is empty.");
//        //            return;
//        //        }

//        //        var msg = msgs[0];

//        //        var rawContactPhone = msg.GetProperty("from").GetString() ?? "";
//        //        var contactPhone = Normalize(rawContactPhone);
//        //        var content = msg.TryGetProperty("text", out var t) && t.TryGetProperty("body", out var b)
//        //            ? b.GetString()
//        //            : null;

//        //        if (!value.TryGetProperty("metadata", out var metadata))
//        //        {
//        //            logger.LogWarning("❌ Inbound payload missing 'metadata' field.");
//        //            return;
//        //        }

//        //        var rawBusinessNumber = metadata.GetProperty("display_phone_number").GetString() ?? "";
//        //        var cleanIncomingBiz = Normalize(rawBusinessNumber);

//        //        logger.LogInformation(
//        //            "🔎 Inbound extract: rawContactPhone={RawContact}, normalizedContact={Contact}, rawBusinessNumber={RawBiz}, normalizedBiz={Biz}",
//        //            rawContactPhone,
//        //            contactPhone,
//        //            rawBusinessNumber,
//        //            cleanIncomingBiz);

//        //        // 2) Resolve business  ✅ now via WhatsAppPhoneNumbers (NOT WhatsAppSettings)
//        //        Guid? businessIdHit = null;

//        //        // Pull active numbers (small table; client-side normalization for reliability)
//        //        var candidates = await db.WhatsAppPhoneNumbers
//        //            .AsNoTracking()
//        //            .Where(n => n.IsActive)
//        //            .Select(n => new { n.BusinessId, n.WhatsAppBusinessNumber })
//        //            .ToListAsync();

//        //        logger.LogDebug("📊 Inbound: Loaded {Count} active WhatsAppPhoneNumbers candidates.", candidates.Count);

//        //        var numHit = candidates.FirstOrDefault(n => Normalize(n.WhatsAppBusinessNumber) == cleanIncomingBiz);
//        //        if (numHit != null)
//        //        {
//        //            businessIdHit = numHit.BusinessId;
//        //            logger.LogInformation(
//        //                "✅ Inbound: resolved BusinessId={BusinessId} for display_phone_number={RawBiz}",
//        //                businessIdHit,
//        //                rawBusinessNumber);
//        //        }

//        //        if (businessIdHit == null || businessIdHit == Guid.Empty)
//        //        {
//        //            logger.LogWarning(
//        //                "❌ Business not found for WhatsApp number: {Number} (normalized={Norm})",
//        //                rawBusinessNumber,
//        //                cleanIncomingBiz);
//        //            return;
//        //        }

//        //        var businessId = businessIdHit.Value;

//        //        // 3) Find or create contact
//        //        logger.LogInformation(
//        //            "👤 Inbound: resolving contact for BusinessId={BusinessId}, Phone={Phone}",
//        //            businessId,
//        //            contactPhone);

//        //        var contact = await contactService.FindOrCreateAsync(businessId, contactPhone);
//        //        if (contact == null)
//        //        {
//        //            logger.LogWarning("❌ Could not resolve contact for phone: {Phone}", contactPhone);
//        //            return;
//        //        }

//        //        logger.LogInformation("✅ Inbound: contact resolved. ContactId={ContactId}", contact.Id);

//        //        // Extract profile name (contacts[0].profile.name) and upsert into Contacts
//        //        static string? TryGetProfileName(JsonElement root)
//        //        {
//        //            if (root.TryGetProperty("contacts", out var contactsEl) &&
//        //                contactsEl.ValueKind == JsonValueKind.Array &&
//        //                contactsEl.GetArrayLength() > 0)
//        //            {
//        //                var c0 = contactsEl[0];
//        //                if (c0.TryGetProperty("profile", out var prof) &&
//        //                    prof.ValueKind == JsonValueKind.Object &&
//        //                    prof.TryGetProperty("name", out var nm) &&
//        //                    nm.ValueKind == JsonValueKind.String)
//        //                {
//        //                    var n = nm.GetString();
//        //                    return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
//        //                }
//        //            }
//        //            return null;
//        //        }

//        //        var profileName = TryGetProfileName(value);
//        //        if (!string.IsNullOrWhiteSpace(profileName))
//        //        {
//        //            try
//        //            {
//        //                logger.LogInformation(
//        //                    "🧾 Inbound: upserting profile name for BusinessId={BusinessId}, Phone={Phone}, Name={Name}",
//        //                    businessId,
//        //                    contactPhone,
//        //                    profileName);

//        //                await contactProfileService.UpsertProfileNameAsync(
//        //                    businessId,
//        //                    contactPhone,
//        //                    profileName!,
//        //                    CancellationToken.None);
//        //            }
//        //            catch (Exception ex)
//        //            {
//        //                logger.LogWarning(ex, "⚠️ Failed to upsert ProfileName for {Phone}", contactPhone);
//        //            }
//        //        }

//        //        // 4) Check chat mode…
//        //        var mode = await chatSessionStateService.GetChatModeAsync(businessId, contact.Id);
//        //        var isAgentMode = mode == "agent";

//        //        logger.LogInformation(
//        //            "💬 Inbound: chat mode for contact {ContactId} is '{Mode}' (isAgentMode={IsAgentMode})",
//        //            contact.Id,
//        //            mode,
//        //            isAgentMode);

//        //        // 5) Log incoming message
//        //        var messageLog = new MessageLog
//        //        {
//        //            Id = Guid.NewGuid(),
//        //            BusinessId = businessId,
//        //            ContactId = contact.Id,
//        //            RecipientNumber = contactPhone,
//        //            MessageContent = content,
//        //            Status = "received",
//        //            CreatedAt = DateTime.UtcNow,
//        //            SentAt = DateTime.UtcNow,
//        //            IsIncoming = true
//        //        };

//        //        db.MessageLogs.Add(messageLog);
//        //        await db.SaveChangesAsync();

//        //        logger.LogInformation(
//        //            "📝 Inbound: MessageLog saved. MessageLogId={MessageLogId}, BusinessId={BusinessId}, ContactId={ContactId}",
//        //            messageLog.Id,
//        //            businessId,
//        //            contact.Id);

//        //        // 6) Notify Inbox clients via SignalR
//        //        try
//        //        {
//        //            var groupName = $"business_{businessId}";
//        //            logger.LogInformation(
//        //                "📡 Inbound: broadcasting ReceiveInboxMessage to SignalR group {GroupName} for ContactId={ContactId}",
//        //                groupName,
//        //                contact.Id);

//        //            await _hub.Clients
//        //                .Group(groupName)
//        //                .SendAsync("ReceiveInboxMessage", new
//        //                {
//        //                    contactId = contact.Id,
//        //                    message = messageLog.MessageContent,
//        //                    isIncoming = true,
//        //                    senderId = (Guid?)null,
//        //                    sentAt = messageLog.CreatedAt
//        //                });
//        //        }
//        //        catch (Exception ex)
//        //        {
//        //            logger.LogWarning(ex, "⚠️ Inbound: failed to broadcast ReceiveInboxMessage to SignalR.");
//        //        }

//        //        // 7) Try to trigger automation by keyword
//        //        try
//        //        {
//        //            var triggerKeyword = (content ?? string.Empty).Trim().ToLowerInvariant();
//        //            logger.LogInformation(
//        //                "⚙️ Inbound: attempting automation flow match for keyword='{Keyword}'",
//        //                triggerKeyword);

//        //            var handled = await automationService.TryRunFlowByKeywordAsync(
//        //                businessId,
//        //                triggerKeyword,
//        //                contact.PhoneNumber,
//        //                sourceChannel: "whatsapp",
//        //                industryTag: "default");

//        //            if (!handled)
//        //                logger.LogInformation("🕵️ No automation flow matched keyword: {Keyword}", triggerKeyword);
//        //            else
//        //                logger.LogInformation("✅ Automation flow handled inbound keyword: {Keyword}", triggerKeyword);
//        //        }
//        //        catch (Exception ex)
//        //        {
//        //            logger.LogError(ex, "❌ Automation flow execution failed.");
//        //        }

//        //        // 8) Sync to inbox only if agent mode
//        //        if (isAgentMode)
//        //        {
//        //            try
//        //            {
//        //                var inboxService = scope.ServiceProvider.GetRequiredService<IInboxService>();

//        //                logger.LogInformation(
//        //                    "📥 Inbound: syncing message to inbox for BusinessId={BusinessId}, ContactId={ContactId}",
//        //                    businessId,
//        //                    contact.Id);

//        //                await inboxService.SaveIncomingMessageAsync(new InboxMessageDto
//        //                {
//        //                    BusinessId = businessId,
//        //                    ContactId = contact.Id,
//        //                    RecipientPhone = contact.PhoneNumber,
//        //                    MessageBody = messageLog.MessageContent,
//        //                    IsIncoming = true,
//        //                    Status = messageLog.Status,
//        //                    SentAt = messageLog.CreatedAt
//        //                });

//        //                logger.LogInformation("✅ Message synced to inbox for contact {Phone}", contactPhone);
//        //            }
//        //            catch (Exception ex)
//        //            {
//        //                logger.LogError(ex, "❌ Failed to sync inbound message to inbox.");
//        //            }
//        //        }
//        //        else
//        //        {
//        //            logger.LogInformation("🚫 Skipping inbox sync: chat mode is not 'agent'");
//        //        }
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        _logger.LogError(ex, "❌ Failed to process inbound WhatsApp chat.");
//        //    }
//        //}

//        public async Task ProcessInteractiveAsync(JsonElement value, CancellationToken ct = default)
//        {
//            _logger.LogInformation("💬 InboundMessageProcessor.ProcessInteractiveAsync started.");

//            using var scope = _serviceScopeFactory.CreateScope();
//            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//            var contactProfileService = scope.ServiceProvider.GetRequiredService<IContactProfileService>();
//            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InboundMessageProcessor>>();

//            static string Normalize(string? number) =>
//                string.IsNullOrWhiteSpace(number) ? "" : new string(number.Where(char.IsDigit).ToArray());

//            // Safe extract of profile name (Meta Cloud shape)
//            static string? TryGetProfileName(JsonElement root)
//            {
//                if (root.TryGetProperty("contacts", out var contactsEl) &&
//                    contactsEl.ValueKind == JsonValueKind.Array &&
//                    contactsEl.GetArrayLength() > 0)
//                {
//                    var c0 = contactsEl[0];
//                    if (c0.TryGetProperty("profile", out var profileEl) &&
//                        profileEl.ValueKind == JsonValueKind.Object &&
//                        profileEl.TryGetProperty("name", out var nameEl) &&
//                        nameEl.ValueKind == JsonValueKind.String)
//                    {
//                        var n = nameEl.GetString();
//                        return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
//                    }
//                }
//                return null;
//            }

//            // messages[0].from is always present for interactive/button
//            if (!value.TryGetProperty("messages", out var msgs) || msgs.GetArrayLength() == 0)
//            {
//                logger.LogWarning("❌ Interactive payload has no 'messages' array or it is empty.");
//                return;
//            }

//            var msg0 = msgs[0];
//            var fromRaw = msg0.GetProperty("from").GetString() ?? "";
//            var fromE164 = Normalize(fromRaw);

//            logger.LogInformation(
//                "🔎 Interactive: fromRaw={FromRaw}, normalized={FromNorm}",
//                fromRaw,
//                fromE164);

//            // Resolve Business via metadata.display_phone_number → WhatsAppPhoneNumbers
//            if (!value.TryGetProperty("metadata", out var metadata))
//            {
//                logger.LogWarning("❌ Interactive payload missing 'metadata' field.");
//                return;
//            }

//            var displayNumberRaw = metadata.GetProperty("display_phone_number").GetString() ?? "";
//            var displayNumber = Normalize(displayNumberRaw);

//            logger.LogInformation(
//                "🔎 Interactive: display_phone_number raw={Raw}, normalized={Norm}",
//                displayNumberRaw,
//                displayNumber);

//            // Look up the business by matching the normalized number in WhatsAppPhoneNumbers
//            var candidates = await db.WhatsAppPhoneNumbers
//                .AsNoTracking()
//                .Where(n => n.IsActive)
//                .Select(n => new { n.BusinessId, n.WhatsAppBusinessNumber })
//                .ToListAsync(ct);

//            logger.LogDebug("📊 Interactive: Loaded {Count} active WhatsAppPhoneNumbers candidates.", candidates.Count);

//            var numHit = candidates.FirstOrDefault(n => Normalize(n.WhatsAppBusinessNumber) == displayNumber);
//            if (numHit == null)
//            {
//                logger.LogWarning(
//                    "❌ Business not found for interactive webhook number: {NumRaw} (normalized={Norm})",
//                    displayNumberRaw,
//                    displayNumber);
//                return;
//            }

//            var businessId = numHit.BusinessId;
//            logger.LogInformation(
//                "✅ Interactive: resolved BusinessId={BusinessId} for number={NumRaw}",
//                businessId,
//                displayNumberRaw);

//            // Upsert profile name if present
//            var profileName = TryGetProfileName(value);
//            if (!string.IsNullOrWhiteSpace(profileName))
//            {
//                try
//                {
//                    logger.LogInformation(
//                        "🧾 Interactive: upserting profile name for BusinessId={BusinessId}, Phone={Phone}, Name={Name}",
//                        businessId,
//                        fromE164,
//                        profileName);

//                    await contactProfileService.UpsertProfileNameAsync(
//                        businessId,
//                        fromE164,
//                        profileName!,
//                        ct);
//                }
//                catch (Exception ex)
//                {
//                    logger.LogWarning(
//                        ex,
//                        "⚠️ Failed to upsert ProfileName on interactive webhook for {Phone}",
//                        fromE164);
//                }
//            }

//            // … your existing interactive handling continues (routing to next step, etc.)
//        }
//    }
//}


////using System;
////using System.Text.Json;
////using System.Threading.Tasks;
////using Microsoft.AspNetCore.SignalR;
////using Microsoft.EntityFrameworkCore;
////using Microsoft.Extensions.Logging;
////using xbytechat.api;
////using xbytechat.api.Features.Inbox.DTOs;
////using xbytechat.api.CRM.Models;
////using xbytechat.api.Features.Inbox.Hubs;
////using Microsoft.Extensions.DependencyInjection;
////using xbytechat.api.CRM.Interfaces;
////using xbytechat.api.Features.AutoReplyBuilder.Services;
////using xbytechat.api.Features.Inbox.Services;
////using xbytechat.api.Features.MessagesEngine.DTOs;
////using xbytechat.api.Features.MessagesEngine.Services;
////using xbytechat.api.CRM.Services;
////using xbytechat.api.Features.Automation.Services;
////using xbytechat.api.Features.Contacts.Services;


////namespace xbytechat.api.Features.Webhooks.Services.Processors
////{
////    public class InboundMessageProcessor : IInboundMessageProcessor
////    {
////        private readonly AppDbContext _context;
////        private readonly IHubContext<InboxHub> _hubContext;
////        private readonly ILogger<InboundMessageProcessor> _logger;
////        private readonly IInboxService _inboxService;
////        private readonly IServiceScopeFactory _serviceScopeFactory;
////        private readonly IHubContext<InboxHub> _hub;
////        private readonly IContactProfileService _contactProfile;
////        public InboundMessageProcessor(
////            AppDbContext context,
////            IHubContext<InboxHub> hubContext,
////            ILogger<InboundMessageProcessor> logger,
////            IInboxService inboxService,
////            IServiceScopeFactory serviceScopeFactory,
////            IHubContext<InboxHub> hub, IContactProfileService contactProfile)
////        {
////            _context = context;
////            _hubContext = hubContext;
////            _logger = logger;
////            _inboxService = inboxService;
////            _serviceScopeFactory = serviceScopeFactory;
////            _hub = hub;
////            _contactProfile = contactProfile;
////        }

////        public async Task ProcessChatAsync(JsonElement value)
////        {
////            try
////            {
////                using var scope = _serviceScopeFactory.CreateScope();
////                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
////                var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();
////                var chatSessionStateService = scope.ServiceProvider.GetRequiredService<IChatSessionStateService>();
////                var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
////                var logger = scope.ServiceProvider.GetRequiredService<ILogger<InboundMessageProcessor>>();
////                var contactProfileService = scope.ServiceProvider.GetRequiredService<IContactProfileService>();

////                // digits-only normalizer (matches how we store/search phones)
////                static string Normalize(string? s) =>
////                    string.IsNullOrWhiteSpace(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

////                // 1) Extract WA metadata + message (Meta Cloud shape)
////                var msg = value.GetProperty("messages")[0];
////                var rawContactPhone = msg.GetProperty("from").GetString() ?? "";
////                var contactPhone = Normalize(rawContactPhone);
////                var content = msg.TryGetProperty("text", out var t) && t.TryGetProperty("body", out var b) ? b.GetString() : null;

////                var rawBusinessNumber = value.GetProperty("metadata").GetProperty("display_phone_number").GetString() ?? "";
////                var cleanIncomingBiz = Normalize(rawBusinessNumber);

////                // 2) Resolve business  ✅ now via WhatsAppPhoneNumbers (NOT WhatsAppSettings)
////                Guid? businessIdHit = null;

////                // Pull active numbers (small table; client-side normalization for reliability)
////                var candidates = await db.WhatsAppPhoneNumbers
////                    .AsNoTracking()
////                    .Where(n => n.IsActive)
////                    .Select(n => new { n.BusinessId, n.WhatsAppBusinessNumber })
////                    .ToListAsync();

////                var numHit = candidates.FirstOrDefault(n => Normalize(n.WhatsAppBusinessNumber) == cleanIncomingBiz);
////                if (numHit != null) businessIdHit = numHit.BusinessId;

////                if (businessIdHit == null || businessIdHit == Guid.Empty)
////                {
////                    logger.LogWarning("❌ Business not found for WhatsApp number: {Number}", rawBusinessNumber);
////                    return;
////                }

////                var businessId = businessIdHit.Value;

////                // 3) Find or create contact
////                var contact = await contactService.FindOrCreateAsync(businessId, contactPhone);
////                if (contact == null)
////                {
////                    logger.LogWarning("❌ Could not resolve contact for phone: {Phone}", contactPhone);
////                    return;
////                }

////                // Extract profile name (contacts[0].profile.name) and upsert into Contacts
////                static string? TryGetProfileName(JsonElement root)
////                {
////                    if (root.TryGetProperty("contacts", out var contactsEl) &&
////                        contactsEl.ValueKind == JsonValueKind.Array &&
////                        contactsEl.GetArrayLength() > 0)
////                    {
////                        var c0 = contactsEl[0];
////                        if (c0.TryGetProperty("profile", out var prof) &&
////                            prof.ValueKind == JsonValueKind.Object &&
////                            prof.TryGetProperty("name", out var nm) &&
////                            nm.ValueKind == JsonValueKind.String)
////                        {
////                            var n = nm.GetString();
////                            return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
////                        }
////                    }
////                    return null;
////                }

////                var profileName = TryGetProfileName(value);
////                if (!string.IsNullOrWhiteSpace(profileName))
////                {
////                    try
////                    {
////                        await contactProfileService.UpsertProfileNameAsync(businessId, contactPhone, profileName!, CancellationToken.None);
////                    }
////                    catch (Exception ex)
////                    {
////                        logger.LogWarning(ex, "⚠️ Failed to upsert ProfileName for {Phone}", contactPhone);
////                    }
////                }

////                // 4) Check chat mode…
////                var mode = await chatSessionStateService.GetChatModeAsync(businessId, contact.Id);
////                var isAgentMode = mode == "agent";

////                // 5) Log incoming message
////                var messageLog = new MessageLog
////                {
////                    Id = Guid.NewGuid(),
////                    BusinessId = businessId,
////                    ContactId = contact.Id,
////                    RecipientNumber = contactPhone,
////                    MessageContent = content,
////                    Status = "received",
////                    CreatedAt = DateTime.UtcNow,
////                    SentAt = DateTime.UtcNow,
////                    IsIncoming = true
////                };

////                db.MessageLogs.Add(messageLog);
////                await db.SaveChangesAsync();

////                await _hub.Clients
////                    .Group($"business_{businessId}")
////                    .SendAsync("ReceiveInboxMessage", new
////                    {
////                        contactId = contact.Id,
////                        message = messageLog.MessageContent,
////                        isIncoming = true,
////                        senderId = (Guid?)null,
////                        sentAt = messageLog.CreatedAt
////                    });

////                // 6) Try to trigger automation by keyword
////                try
////                {
////                    var triggerKeyword = (content ?? string.Empty).Trim().ToLowerInvariant();
////                    var handled = await automationService.TryRunFlowByKeywordAsync(
////                        businessId,
////                        triggerKeyword,
////                        contact.PhoneNumber,
////                        sourceChannel: "whatsapp",
////                        industryTag: "default");

////                    if (!handled)
////                        logger.LogInformation("🕵️ No automation flow matched keyword: {Keyword}", triggerKeyword);
////                }
////                catch (Exception ex)
////                {
////                    logger.LogError(ex, "❌ Automation flow execution failed.");
////                }

////                // 7) Sync to inbox only if agent mode
////                if (isAgentMode)
////                {
////                    var inboxService = scope.ServiceProvider.GetRequiredService<IInboxService>();
////                    await inboxService.SaveIncomingMessageAsync(new InboxMessageDto
////                    {
////                        BusinessId = businessId,
////                        ContactId = contact.Id,
////                        RecipientPhone = contact.PhoneNumber,
////                        MessageBody = messageLog.MessageContent,
////                        IsIncoming = true,
////                        Status = messageLog.Status,
////                        SentAt = messageLog.CreatedAt
////                    });

////                    logger.LogInformation("📥 Message synced to inbox for contact {Phone}", contactPhone);
////                }
////                else
////                {
////                    logger.LogInformation("🚫 Skipping inbox sync: chat mode is not 'agent'");
////                }
////            }
////            catch (Exception ex)
////            {
////                _logger.LogError(ex, "❌ Failed to process inbound WhatsApp chat.");
////            }
////        }

////        public async Task ProcessInteractiveAsync(JsonElement value, CancellationToken ct = default)
////        {
////            using var scope = _serviceScopeFactory.CreateScope();
////            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
////            var contactProfileService = scope.ServiceProvider.GetRequiredService<IContactProfileService>();
////            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InboundMessageProcessor>>();

////            static string Normalize(string? number) =>
////                string.IsNullOrWhiteSpace(number) ? "" : new string(number.Where(char.IsDigit).ToArray());

////            // Safe extract of profile name (Meta Cloud shape)
////            static string? TryGetProfileName(JsonElement root)
////            {
////                if (root.TryGetProperty("contacts", out var contactsEl) &&
////                    contactsEl.ValueKind == JsonValueKind.Array &&
////                    contactsEl.GetArrayLength() > 0)
////                {
////                    var c0 = contactsEl[0];
////                    if (c0.TryGetProperty("profile", out var profileEl) &&
////                        profileEl.ValueKind == JsonValueKind.Object &&
////                        profileEl.TryGetProperty("name", out var nameEl) &&
////                        nameEl.ValueKind == JsonValueKind.String)
////                    {
////                        var n = nameEl.GetString();
////                        return string.IsNullOrWhiteSpace(n) ? null : n!.Trim();
////                    }
////                }
////                return null;
////            }

////            // messages[0].from is always present for interactive/button
////            if (!value.TryGetProperty("messages", out var msgs) || msgs.GetArrayLength() == 0)
////                return;

////            var msg0 = msgs[0];
////            var fromRaw = msg0.GetProperty("from").GetString() ?? "";
////            var fromE164 = Normalize(fromRaw);

////            // Resolve Business via metadata.display_phone_number → WhatsAppPhoneNumbers
////            var displayNumberRaw = value.GetProperty("metadata").GetProperty("display_phone_number").GetString() ?? "";
////            var displayNumber = Normalize(displayNumberRaw);

////            // Look up the business by matching the normalized number in WhatsAppPhoneNumbers
////            var candidates = await db.WhatsAppPhoneNumbers
////                .AsNoTracking()
////                .Where(n => n.IsActive)
////                .Select(n => new { n.BusinessId, n.WhatsAppBusinessNumber })
////                .ToListAsync(ct);

////            var numHit = candidates.FirstOrDefault(n => Normalize(n.WhatsAppBusinessNumber) == displayNumber);
////            if (numHit == null)
////            {
////                logger.LogWarning("❌ Business not found for interactive webhook number: {Num}", displayNumberRaw);
////                return;
////            }

////            var businessId = numHit.BusinessId;

////            // Upsert profile name if present
////            var profileName = TryGetProfileName(value);
////            if (!string.IsNullOrWhiteSpace(profileName))
////            {
////                try
////                {
////                    await contactProfileService.UpsertProfileNameAsync(businessId, fromE164, profileName!, ct);
////                }
////                catch (Exception ex)
////                {
////                    logger.LogWarning(ex, "⚠️ Failed to upsert ProfileName on interactive webhook for {Phone}", fromE164);
////                }
////            }

////            // … continue your existing interactive handling (routing to next step, etc.)
////        }

////    }
////}
