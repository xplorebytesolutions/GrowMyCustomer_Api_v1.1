using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Webhooks.Directory;
using xbytechat.api.Features.Webhooks.Pinnacle.Services.Adapters;
using xbytechat.api.Features.Webhooks.Services.Processors;
using xbytechat_api.Features.Billing.Services;

namespace xbytechat.api.Features.Webhooks.Services
{
    public class WhatsAppWebhookDispatcher : IWhatsAppWebhookDispatcher
    {
        // Removed legacy _statusProcessor on purpose
        private readonly ITemplateWebhookProcessor _templateProcessor;
        private readonly IClickWebhookProcessor _clickProcessor;
        private readonly IInboundMessageProcessor _inboundMessageProcessor;
        private readonly IWhatsAppWebhookService _webhookService;
        private readonly IProviderDirectory _directory;
        private readonly ILogger<WhatsAppWebhookDispatcher> _logger;
        private readonly IPinnacleToMetaAdapter _pinnacleToMetaAdapter;
        private readonly IBillingIngestService _billingIngest;

        public WhatsAppWebhookDispatcher(
            ITemplateWebhookProcessor templateProcessor,
            ILogger<WhatsAppWebhookDispatcher> logger,
            IClickWebhookProcessor clickProcessor,
            IInboundMessageProcessor inboundMessageProcessor,
            IWhatsAppWebhookService webhookService,
            IProviderDirectory directory,
            IPinnacleToMetaAdapter pinnacleToMetaAdapter,
            IBillingIngestService billingIngest)
        {
            _templateProcessor = templateProcessor;
            _logger = logger;
            _clickProcessor = clickProcessor;
            _inboundMessageProcessor = inboundMessageProcessor;
            _webhookService = webhookService;
            _directory = directory;
            _pinnacleToMetaAdapter = pinnacleToMetaAdapter;
            _billingIngest = billingIngest;
        }

        public async Task DispatchAsync(JsonElement payload)
        {
            // Keep raw payload at Debug to avoid log spam in prod
            _logger.LogDebug("📦 Dispatcher raw payload:\n{Payload}", payload.GetRawText());

            try
            {
                // 0) Detect provider & normalize into a Meta-like "entry[].changes[].value" envelope
                var provider = DetectProvider(payload); // "meta" | "pinnacle" | null
                _logger.LogInformation("🌐 Dispatcher: detected provider={Provider}", provider ?? "(auto/meta)");

                var envelope = provider == "pinnacle"
                    ? _pinnacleToMetaAdapter.ToMetaEnvelope(payload)
                    : payload;

                if (!envelope.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("⚠️ Dispatcher: No 'entry' array found on envelope; skipping payload.");
                    return;
                }

                // Compute once per envelope (micro-optimization)
                var isStatus = IsStatusPayload(envelope);
                _logger.LogInformation("🔎 Dispatcher: isStatusPayload={IsStatus}", isStatus);

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    {
                        _logger.LogDebug("ℹ️ Dispatcher: 'entry' without 'changes' array; skipping entry.");
                        continue;
                    }

                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                        {
                            _logger.LogDebug("ℹ️ Dispatcher: 'change' without object 'value'; skipping change.");
                            continue;
                        }

                        // 1) STATUS UPDATES
                        if (isStatus)
                        {
                            _logger.LogInformation("📦 Dispatcher: treating envelope as STATUS payload (provider={Provider}).", provider ?? "meta");

                            // Resolve BusinessId using *envelope* metadata (works for Meta and adapted Pinnacle)
                            Guid resolvedBiz = Guid.Empty;
                            try
                            {
                                var hints = ExtractNumberHints(envelope, provider);
                                _logger.LogDebug(
                                    "🔢 Dispatcher: Number hints extracted. PhoneNumberId={PhoneNumberId}, DisplayPhone={DisplayPhone}, WabaId={WabaId}, WaId={WaId}",
                                    hints.PhoneNumberId,
                                    hints.DisplayPhoneNumber,
                                    hints.WabaId,
                                    hints.WaId);

                                var bid = await _directory.ResolveBusinessIdAsync(
                                    provider: provider,
                                    phoneNumberId: hints.PhoneNumberId,
                                    displayPhoneNumber: hints.DisplayPhoneNumber,
                                    wabaId: hints.WabaId,
                                    waId: hints.WaId
                                );
                                if (bid.HasValue) resolvedBiz = bid.Value;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "ProviderDirectory lookup failed; proceeding without BusinessId.");
                            }

                            // Canonical provider label for billing
                            var providerCanonical = string.Equals(provider, "pinnacle", StringComparison.OrdinalIgnoreCase)
                                ? "PINNACLE"
                                : "META_CLOUD";

                            // Only call billing ingest when BusinessId was resolved
                            if (resolvedBiz != Guid.Empty)
                            {
                                try
                                {
                                    _logger.LogInformation(
                                        "💰 Dispatcher: routing status payload to BillingIngest for BusinessId={BusinessId}, Provider={ProviderCanonical}",
                                        resolvedBiz,
                                        providerCanonical);

                                    await _billingIngest.IngestFromWebhookAsync(
                                        resolvedBiz,
                                        providerCanonical,
                                        envelope.GetRawText());
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Billing ingest from webhook failed (non-fatal).");
                                }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "⚠️ Dispatcher: status payload had no resolved BusinessId; billing ingest will be skipped.");
                            }

                            // Unified status updater (no legacy fallback)
                            _logger.LogInformation(
                                "📦 Dispatcher: routing to Unified Status Updater (provider={Provider}, businessId={BusinessId})",
                                provider,
                                resolvedBiz == Guid.Empty ? "(unknown)" : resolvedBiz.ToString());

                            await _webhookService.ProcessStatusUpdateAsync(
                                resolvedBiz,
                                provider ?? "meta",
                                envelope);

                            // Note: continue to next change — even if envelope is status-oriented,
                            // other changes in the same webhook could be non-status in some providers.
                            continue;
                        }

                        // 2) TEMPLATE EVENTS
                        if (value.TryGetProperty("event", out var eventType) &&
                            eventType.GetString()?.StartsWith("template_", StringComparison.Ordinal) == true)
                        {
                            _logger.LogInformation("📦 Dispatcher: routing to Template Processor (event={Event})", eventType.GetString());
                            await _templateProcessor.ProcessTemplateUpdateAsync(envelope);
                            continue;
                        }

                        // 3) MESSAGES (clicks + inbound)
                        if (!value.TryGetProperty("messages", out var msgs) || msgs.GetArrayLength() == 0)
                        {
                            _logger.LogDebug("ℹ️ Dispatcher: 'value' has no 'messages' array or it is empty; skipping.");
                            continue;
                        }

                        foreach (var m in msgs.EnumerateArray())
                        {
                            if (!m.TryGetProperty("type", out var typeProp))
                            {
                                _logger.LogDebug("ℹ️ Dispatcher: message without 'type' field; skipping message.");
                                continue;
                            }

                            var type = typeProp.GetString();
                            _logger.LogDebug("🔍 Dispatcher: inspecting message of type '{Type}'.", type);

                            // (A) Legacy quick-reply button → CLICK
                            if (type == "button")
                            {
                                _logger.LogInformation("👉 Dispatcher: routing to Click Processor (legacy 'button').");
                                await _clickProcessor.ProcessClickAsync(value);
                                continue;
                            }

                            // (B) Interactive (button_reply / list_reply) → CLICK
                            if (type == "interactive" && m.TryGetProperty("interactive", out var interactive))
                            {
                                if (interactive.TryGetProperty("type", out var interactiveType) &&
                                    interactiveType.GetString() == "button_reply")
                                {
                                    _logger.LogInformation("👉 Dispatcher: routing to Click Processor (interactive/button_reply).");
                                    await _clickProcessor.ProcessClickAsync(value);
                                    continue;
                                }

                                if (interactive.TryGetProperty("list_reply", out _))
                                {
                                    _logger.LogInformation("👉 Dispatcher: routing to Click Processor (interactive/list_reply).");
                                    await _clickProcessor.ProcessClickAsync(value);
                                    continue;
                                }
                            }

                            // (C) Inbound plain message types → INBOUND
                            if (type is "text" or "image" or "audio")
                            {
                                _logger.LogInformation(
                                    "💬 Dispatcher: routing to InboundMessageProcessor (message type: {Type}, provider={Provider}).",
                                    type,
                                    provider ?? "meta");

                                await _inboundMessageProcessor.ProcessChatAsync(value);
                                continue;
                            }

                            _logger.LogDebug("ℹ️ Dispatcher: message type '{Type}' not handled by dispatcher.", type);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Dispatcher failed to process WhatsApp webhook.");
            }
        }

        private static bool IsStatusPayload(JsonElement root)
        {
            // Meta-like: entry[].changes[].value.statuses
            if (TryGetMetaValue(root, out var val) && val.Value.TryGetProperty("statuses", out _))
                return true;

            // Some providers mark with "status" or event containing "status"
            if (root.TryGetProperty("status", out _)) return true;
            if (root.TryGetProperty("event", out var ev) &&
                (ev.GetString()?.Contains("status", StringComparison.OrdinalIgnoreCase) ?? false))
                return true;

            return false;
        }

        private static string? DetectProvider(JsonElement root)
        {
            // Heuristics by envelope
            if (root.TryGetProperty("object", out var obj) && obj.GetString() == "whatsapp_business_account")
                return "meta";
            if (root.TryGetProperty("entry", out _))
                return "meta";
            if (root.TryGetProperty("event", out _))
                return "pinnacle";
            return null;
        }

        private static bool TryGetMetaValue(JsonElement root, out (JsonElement Value, JsonElement? Change, JsonElement? Entry) res)
        {
            res = default;
            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
                return false;

            var entry = entries[0];
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array || changes.GetArrayLength() == 0)
                return false;

            var change = changes[0];
            if (!change.TryGetProperty("value", out var value))
                return false;

            res = (value, change, entry);
            return true;
        }

        private static NumberHints ExtractNumberHints(JsonElement root, string? provider)
        {
            var hints = new NumberHints();

            // Meta (or unknown → treat as Meta envelope)
            if (string.Equals(provider, "meta", StringComparison.OrdinalIgnoreCase) || provider is null)
            {
                if (TryGetMetaValue(root, out var v))
                {
                    if (v.Value.TryGetProperty("metadata", out var md))
                    {
                        if (md.TryGetProperty("phone_number_id", out var pnid))
                            hints.PhoneNumberId = pnid.GetString();

                        if (md.TryGetProperty("display_phone_number", out var disp))
                            hints.DisplayPhoneNumber = NormalizePhone(disp.GetString());

                        if (md.TryGetProperty("waba_id", out var wabaFromMeta))
                            hints.WabaId = wabaFromMeta.GetString();
                    }

                    // Some adapters put waba_id at value-level
                    if (string.IsNullOrWhiteSpace(hints.WabaId) &&
                        v.Value.TryGetProperty("waba_id", out var wabaTop))
                    {
                        hints.WabaId = wabaTop.GetString();
                    }

                    // First status often carries recipient_id (WA ID)
                    if (v.Value.TryGetProperty("statuses", out var statuses) &&
                        statuses.ValueKind == JsonValueKind.Array && statuses.GetArrayLength() > 0)
                    {
                        var s0 = statuses[0];
                        if (s0.TryGetProperty("recipient_id", out var rid))
                            hints.WaId = rid.GetString();
                    }
                }
            }
            // Pinnacle (raw or adapted)
            else if (string.Equals(provider, "pinnacle", StringComparison.OrdinalIgnoreCase))
            {
                // If your adapter produced a Meta-like envelope, this will work too:
                if (TryGetMetaValue(root, out var v2) && v2.Value.TryGetProperty("metadata", out var md2))
                {
                    if (md2.TryGetProperty("phone_number_id", out var pnid2))
                        hints.PhoneNumberId = pnid2.GetString();

                    if (md2.TryGetProperty("display_phone_number", out var disp2))
                        hints.DisplayPhoneNumber = NormalizePhone(disp2.GetString());
                }

                // Raw Pinnacle-style fields on the envelope
                if (string.IsNullOrWhiteSpace(hints.PhoneNumberId) &&
                    root.TryGetProperty("phone_number_id", out var pn))
                    hints.PhoneNumberId = pn.GetString();

                if (string.IsNullOrWhiteSpace(hints.DisplayPhoneNumber))
                {
                    if (root.TryGetProperty("from", out var from))
                        hints.DisplayPhoneNumber = NormalizePhone(from.GetString());
                    else if (root.TryGetProperty("msisdn", out var msisdn))
                        hints.DisplayPhoneNumber = NormalizePhone(msisdn.GetString());
                }

                if (root.TryGetProperty("wabaId", out var waba))
                    hints.WabaId = waba.GetString();
            }

            return hints;
        }

        private static string? NormalizePhone(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            var t = v.Trim();
            var keepPlus = t.StartsWith("+");
            var digits = new string(t.Where(char.IsDigit).ToArray());
            return keepPlus ? "+" + digits : digits;
        }

        private struct NumberHints
        {
            public string? PhoneNumberId { get; set; }
            public string? DisplayPhoneNumber { get; set; }
            public string? WabaId { get; set; }
            public string? WaId { get; set; }
        }
    }
}







