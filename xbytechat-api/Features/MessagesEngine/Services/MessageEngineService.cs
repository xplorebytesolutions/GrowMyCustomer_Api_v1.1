// 📄 File: Features/MessagesEngine/Services/MessageEngineService.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Meta;
using xbytechat.api.Features.CampaignModule.SendEngine.PayloadModels.Pinnacle;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.Inbox.Hubs;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.MessagesEngine.Abstractions;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Enums;
using xbytechat.api.Features.MessagesEngine.Factory;
using xbytechat.api.Features.MessagesEngine.PayloadBuilders;
using xbytechat.api.Features.PlanManagement.Services;
using xbytechat.api.Features.ReportingModule.DTOs;
using xbytechat.api.Features.Webhooks.Services.Resolvers;
using xbytechat.api.Helpers;
using xbytechat.api.Infrastructure.Json;         // <- source-gen context (JsonCtx)
using xbytechat.api.Shared;
using xbytechat.api.Shared.utility;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.Features.MessagesEngine.Services
{
    public class MessageEngineService : IMessageEngineService
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _http; // kept for any internal calls
        private readonly TextMessagePayloadBuilder _textBuilder;
        private readonly ImageMessagePayloadBuilder _imageBuilder;
        private readonly TemplateMessagePayloadBuilder _templateBuilder;
        private readonly CtaMessagePayloadBuilder _ctaBuilder;
        private readonly IPlanManager _planManager;
        private readonly IHubContext<InboxHub> _hubContext;
        private readonly IMessageIdResolver _messageIdResolver;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IContactService _contactService;
        private readonly IWhatsAppProviderFactory _providerFactory;
        private readonly ILogger<MessageEngineService> _logger;

        // Basic cache for WhatsApp settings to reduce DB load
        private readonly ConcurrentDictionary<Guid, (IReadOnlyList<WhatsAppSettingEntity> setting, DateTime expiresAt)> _settingsCache = new();

        public MessageEngineService(
            AppDbContext db,
            HttpClient http,
            TextMessagePayloadBuilder textBuilder,
            ImageMessagePayloadBuilder imageBuilder,
            TemplateMessagePayloadBuilder templateBuilder,
            CtaMessagePayloadBuilder ctaBuilder,
            IPlanManager planManager,
            IHubContext<InboxHub> hubContext,
            IMessageIdResolver messageIdResolver,
            IHttpContextAccessor httpContextAccessor,
            IContactService contactService,
            IWhatsAppProviderFactory providerFactory,
            ILogger<MessageEngineService> logger)
        {
            _db = db;
            _http = http;
            _textBuilder = textBuilder;
            _imageBuilder = imageBuilder;
            _templateBuilder = templateBuilder;
            _ctaBuilder = ctaBuilder;
            _planManager = planManager;
            _hubContext = hubContext;
            _messageIdResolver = messageIdResolver;
            _httpContextAccessor = httpContextAccessor;
            _contactService = contactService;
            _providerFactory = providerFactory;
            _logger = logger;
        }

        // ---------- small helpers ----------
        private static string ResolveGreeting(string? profileName, string? contactName)
        {
            var s = (profileName ?? contactName)?.Trim();
            return string.IsNullOrEmpty(s) ? "there" : s;
        }

        private static void EnsureArgsLength(List<string> args, int slot1Based)
        {
            while (args.Count < slot1Based) args.Add(string.Empty);
        }

        // ✅ Public helper so both Flow + Campaign send paths can use it
        //public async Task<List<string>> ApplyProfileNameAsync(
        //    Guid businessId,
        //    Guid contactId,
        //    bool useProfileName,
        //    int? profileNameSlot,
        //    List<string> args,
        //    CancellationToken ct = default)
        //{
        //    if (!useProfileName || !(profileNameSlot is int slot) || slot < 1)
        //        return args;

        //    //var contact = await _db.Contacts
        //    //    .AsNoTracking()
        //    //    .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.Id == contactId, ct);

        //    var greet = ResolveGreeting(contact?.ProfileName, contact?.Name);
        //    EnsureArgsLength(args, slot);
        //    args[slot - 1] = greet;
        //    return args;
        //}
        public async Task<List<string>> ApplyProfileNameAsync(
    Guid businessId,
    Guid contactId,
    bool useProfileName,
    int? profileNameSlot,
    List<string> args,
    CancellationToken ct = default)
        {
            // Normalize args so we never return null
            args ??= new List<string>();

            // Quick outs
            if (!useProfileName || profileNameSlot is not int slot || slot < 1)
                return args;

            // Load the contact only when needed
            var contact = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.BusinessId == businessId && c.Id == contactId)
                .Select(c => new { c.Name, c.ProfileName })
                .FirstOrDefaultAsync(ct);

            // If no contact, just return args unchanged
            if (contact is null)
                return args;

            // Build the greeting / display name (adjust ResolveGreeting to your needs)
            var greet = ResolveGreeting(contact.ProfileName, contact.Name);
            if (string.IsNullOrWhiteSpace(greet))
                return args; // nothing to apply

            // Ensure args has capacity for the requested slot (1-based)
            if (args.Count < slot)
                args.AddRange(Enumerable.Repeat(string.Empty, slot - args.Count));

            // Set the value at the requested slot
            args[slot - 1] = greet;

            return args;
        }

        // ============================================================
        //  SOURCE-GEN PATH FOR TYPED PAYLOADS (Step 9 – point #5)
        // ============================================================
        public async Task<ResponseResult> SendPayloadAsync(Guid businessId, string provider, object payload, string? phoneNumberId = null)
        {
            if (string.IsNullOrWhiteSpace(provider) || (provider != "PINNACLE" && provider != "META_CLOUD"))
                return ResponseResult.ErrorInfo("❌ Invalid provider.", "Provider must be exactly 'PINNACLE' or 'META_CLOUD'.");

            // If already-typed, keep your current JsonElement path:
            if (payload is MetaTemplateMessage m)
            {
                var json = JsonSerializer.Serialize(m, JsonCtx.Default.MetaTemplateMessage);
                using var doc = JsonDocument.Parse(json);
                return await SendViaProviderAsync(businessId, provider, p => p.SendInteractiveAsync(doc.RootElement.Clone()), phoneNumberId);
            }
            if (payload is PinnacleTemplateMessage pmsg)
            {
                var json = JsonSerializer.Serialize(pmsg, JsonCtx.Default.PinnacleTemplateMessage);
                using var doc = JsonDocument.Parse(json);
                return await SendViaProviderAsync(businessId, provider, p => p.SendInteractiveAsync(doc.RootElement.Clone()), phoneNumberId);
            }

            // NEW: if the anonymous payload looks like a WhatsApp "template" message,
            // extract the parts and call SendTemplateAsync directly (no raw object pass-through).
            if (payload is JsonElement je && je.ValueKind == JsonValueKind.Object &&
                je.TryGetProperty("type", out var t) && t.GetString() == "template" &&
                je.TryGetProperty("to", out var toProp) &&
                je.TryGetProperty("template", out var tmpl) &&
                tmpl.TryGetProperty("name", out var nameProp) &&
                tmpl.TryGetProperty("language", out var langProp) &&
                langProp.TryGetProperty("code", out var codeProp) &&
                tmpl.TryGetProperty("components", out var comps))
            {
                var to = toProp.GetString()!;
                var name = nameProp.GetString()!;
                var code = codeProp.GetString()!;
                // Materialize components as plain anonymous objects to guarantee no $type:
                var components = new List<object>();
                foreach (var c in comps.EnumerateArray())
                {
                    var type = c.GetProperty("type").GetString();
                    if (type == "body")
                    {
                        var pars = c.TryGetProperty("parameters", out var pr)
                            ? pr.EnumerateArray().Select(p => new { type = p.GetProperty("type").GetString(), text = p.GetProperty("text").GetString() }).ToArray()
                            : System.Array.Empty<object>();
                        components.Add(new { type = "body", parameters = pars });
                    }
                    else if (type == "header")
                    {
                        // support header image
                        if (c.TryGetProperty("parameters", out var pr) && pr.GetArrayLength() > 0)
                        {
                            var p0 = pr[0];
                            if (p0.TryGetProperty("type", out var pt) && pt.GetString() == "image")
                            {
                                var link = p0.GetProperty("image").GetProperty("link").GetString();
                                components.Add(new { type = "header", parameters = new object[] { new { type = "image", image = new { link } } } });
                            }
                            else
                            {
                                components.Add(new { type = "header", parameters = new object[] { } });
                            }
                        }
                        else components.Add(new { type = "header", parameters = new object[] { } });
                    }
                    else if (type == "button")
                    {
                        var subType = c.GetProperty("sub_type").GetString();
                        var index = c.GetProperty("index").GetString();
                        if (subType == "url" && c.TryGetProperty("parameters", out var pr) && pr.GetArrayLength() > 0)
                        {
                            var urlParam = pr[0].GetProperty("text").GetString();
                            components.Add(new { type = "button", sub_type = "url", index, parameters = new object[] { new { type = "text", text = urlParam } } });
                        }
                    }
                }

                return await SendViaProviderAsync(businessId, provider,
                    p => p.SendTemplateAsync(to, name, code, components),
                    phoneNumberId);
            }

            // Fallback: send as-is via interactive
            return await SendViaProviderAsync(businessId, provider, p => p.SendInteractiveAsync(payload), phoneNumberId);
        }





        private static string NormalizeProviderOrThrow(string? p)
        {
            if (string.IsNullOrWhiteSpace(p))
                throw new ArgumentException("Provider is required.");

            var u = p.Trim().ToUpperInvariant();
            return u switch
            {
                "META" => "META_CLOUD", // internal convenience; callers should still pass exact values
                "META_CLOUD" => "META_CLOUD",
                "PINNACLE" => "PINNACLE",
                _ => throw new ArgumentException($"Invalid provider: {p}")
            };
        }

        private async Task<ResponseResult> SendViaProviderAsync(
            Guid businessId,
            string provider,                                // explicit
            Func<IWhatsAppProvider, Task<WaSendResult>> action,
            string? phoneNumberId = null)
        {
            try
            {
                // normalize internally (tolerate "META" here) but keep external API strict
                var normalizedProvider = NormalizeProviderOrThrow(provider);

                // For both META_CLOUD and PINNACLE we require a sender id here
                if (string.IsNullOrWhiteSpace(phoneNumberId))
                    return ResponseResult.ErrorInfo(
                        "❌ Campaign has no sender number.",
                        "Missing PhoneNumberId");

                // Build provider bound to business + sender
                var wa = await _providerFactory.CreateAsync(
                    businessId,
                    normalizedProvider,
                    phoneNumberId);

                // post request to http URL
                var response = await action(wa);

                if (!response.Success)
                    return ResponseResult.ErrorInfo("❌ WhatsApp API returned an error.", response.Error, response.RawResponse);

                var rr = ResponseResult.SuccessInfo("✅ Message sent successfully", data: null, raw: response.RawResponse);
                rr.MessageId = string.IsNullOrWhiteSpace(response.ProviderMessageId)
                    ? TryExtractMetaWamid(response.RawResponse)
                    : response.ProviderMessageId;
                return rr;
            }
            catch (ArgumentException ex)
            {
                return ResponseResult.ErrorInfo("❌ Invalid provider.", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ResponseResult.ErrorInfo("❌ Provider configuration error.", ex.Message);
            }
            catch (Exception ex)
            {
                return ResponseResult.ErrorInfo("❌ Provider call failed.", ex.Message);
            }
        }

        private static string? TryExtractMetaWamid(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.TrimStart();
            if (!s.StartsWith("{")) return null;
            try
            {
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.TryGetProperty("messages", out var msgs) &&
                    msgs.ValueKind == JsonValueKind.Array &&
                    msgs.GetArrayLength() > 0 &&
                    msgs[0].TryGetProperty("id", out var idProp))
                {
                    return idProp.GetString();
                }
            }
            catch { }
            return null;
        }

        // ---------- CSV-materialized variable helpers (for campaign recipients) ----------
        private static string[] ReadBodyParams(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }

        private static Dictionary<string, string> ReadVarDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static List<string> BuildHeaderTextParams(IDictionary<string, string> kv)
        {
            var matches = kv.Keys
                .Select(k => new
                {
                    k,
                    m = System.Text.RegularExpressions.Regex.Match(
                        k, @"^(?:header(?:\.text)?\.)?(\d+)$|^header(?:\.text)?\.(\d+)$|^headerpara(\d+)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                })
                .Where(x => x.m.Success)
                .Select(x =>
                {
                    for (int g = 1; g < x.m.Groups.Count; g++)
                        if (x.m.Groups[g].Success) return int.Parse(x.m.Groups[g].Value);
                    return 0;
                })
                .Where(n => n > 0)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (matches.Count == 0) return new List<string>();

            var list = new List<string>(new string[matches.Last()]);
            for (int i = 1; i <= list.Count; i++)
            {
                var k1 = $"header.text.{i}";
                var k2 = $"headerpara{i}";
                if (!kv.TryGetValue(k1, out var v))
                    kv.TryGetValue(k2, out v);
                list[i - 1] = v ?? string.Empty;
            }

            return list;
        }

        private static IReadOnlyDictionary<string, string> BuildButtonUrlParams(IDictionary<string, string> kv)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int pos = 1; pos <= 3; pos++)
            {
                var k1 = $"button{pos}.url_param";
                var k2 = $"buttonpara{pos}";
                if (kv.TryGetValue(k1, out var v1) && !string.IsNullOrWhiteSpace(v1))
                    map[k1] = v1;
                else if (kv.TryGetValue(k2, out var v2) && !string.IsNullOrWhiteSpace(v2))
                    map[k1] = v2;
            }
            return map;
        }

        // ======================================================================
        //  SEND METHODS (kept from your file; minor tidy + consistent responses)
        // ======================================================================

        public async Task<ResponseResult> SendTemplateMessageAsync(SendMessageDto dto)
        {
            try
            {
                Console.WriteLine($"📨 Sending template message to {dto.RecipientNumber} via BusinessId {dto.BusinessId}");

                if (dto.MessageType != MessageTypeEnum.Template)
                    return ResponseResult.ErrorInfo("Only template messages are supported in this method.");

                // strict provider check at API surface
                if (string.IsNullOrWhiteSpace(dto.Provider) ||
                    (dto.Provider != "PINNACLE" && dto.Provider != "META_CLOUD"))
                {
                    return ResponseResult.ErrorInfo("❌ Invalid provider.",
                        "Provider must be exactly 'PINNACLE' or 'META_CLOUD'.");
                }

                // Quota
                var quotaCheck = await _planManager.CheckQuotaBeforeSendingAsync(dto.BusinessId);
                if (!quotaCheck.Success) return quotaCheck;

                // Build components (body only here)
                var bodyParams = (dto.TemplateParameters?.Values?.ToList() ?? new List<string>())
                    .Select(p => new { type = "text", text = p })
                    .ToArray();

                var components = new List<object>();
                if (bodyParams.Length > 0)
                    components.Add(new { type = "body", parameters = bodyParams });

                // Send via provider
                var sendResult = await SendViaProviderAsync(
                    dto.BusinessId,
                    dto.Provider,
                    p => p.SendTemplateAsync(dto.RecipientNumber, dto.TemplateName!, "en_US", components),
                    dto.PhoneNumberId
                );

                // Rendered body (for logs)
                var resolvedBody = TemplateParameterHelper.FillPlaceholders(
                    dto.TemplateBody ?? "",
                    dto.TemplateParameters?.Values.ToList() ?? new List<string>());

                // Log result
                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TemplateName ?? "N/A",
                    RenderedBody = resolvedBody,
                    MediaUrl = null,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId,
                };

                await _db.MessageLogs.AddAsync(log);
                var planInfo = await _db.BusinessPlanInfos.FirstOrDefaultAsync(p => p.BusinessId == dto.BusinessId);
                if (planInfo != null && planInfo.RemainingMessages > 0)
                {
                    planInfo.RemainingMessages -= 1;
                    planInfo.UpdatedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync();

                // SignalR push
                await _hubContext.Clients
                    .Group($"business_{dto.BusinessId}")
                    .SendAsync("ReceiveMessage", new
                    {
                        Id = log.Id,
                        RecipientNumber = log.RecipientNumber,
                        MessageContent = log.RenderedBody,
                        MediaUrl = log.MediaUrl,
                        Status = log.Status,
                        CreatedAt = log.CreatedAt,
                        SentAt = log.SentAt
                    });

                return ResponseResult.SuccessInfo("✅ Template message sent successfully.", sendResult, log.RawResponse);
            }
            catch (Exception ex)
            {
                var errorId = Guid.NewGuid();
                Console.WriteLine($"🧨 Error ID: {errorId}\n{ex}");

                await _db.MessageLogs.AddAsync(new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TemplateName ?? "N/A",
                    RenderedBody = TemplateParameterHelper.FillPlaceholders(
                        dto.TemplateBody ?? "",
                        dto.TemplateParameters?.Values.ToList() ?? new List<string>()),
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    RawResponse = ex.ToString(),
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                return ResponseResult.ErrorInfo(
                    $"❌ Exception occurred while sending template message. [Ref: {errorId}]",
                    ex.ToString());
            }
        }
        [Obsolete("Use outbox + SendPayloadAsync via worker.")]
        public async Task<ResponseResult> SendVideoTemplateMessageAsync(VideoTemplateMessageDto dto, Guid businessId)
        {
            try
            {
                var provider = (dto.Provider ?? "META_CLOUD").Trim().ToUpperInvariant();
                if (provider != "PINNACLE" && provider != "META_CLOUD")
                    return ResponseResult.ErrorInfo("❌ Invalid provider.", "Provider must be exactly 'PINNACLE' or 'META_CLOUD'.");

                if (string.IsNullOrWhiteSpace(dto.RecipientNumber))
                    return ResponseResult.ErrorInfo("❌ Missing recipient number.");
                if (string.IsNullOrWhiteSpace(dto.TemplateName))
                    return ResponseResult.ErrorInfo("❌ Missing template name.");
                if (string.IsNullOrWhiteSpace(dto.HeaderVideoUrl))
                    return ResponseResult.ErrorInfo("🚫 Missing HeaderVideoUrl (must be a direct HTTPS link to a video file).");

                var langCode = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode!.Trim();

                // components: header video + body + optional buttons
                var components = new List<object>
                {
                    new
                    {
                        type = "header",
                        parameters = new object[]
                        {
                            new { type = "video", video = new { link = dto.HeaderVideoUrl! } }
                        }
                    }
                };

                var bodyParams = (dto.TemplateParameters ?? new List<string>())
                    .Select(p => new { type = "text", text = p })
                    .ToArray();

                components.Add(new { type = "body", parameters = bodyParams });

                var btns = (dto.ButtonParameters ?? new List<CampaignButtonDto>()).Take(3).ToList();
                for (int i = 0; i < btns.Count; i++)
                {
                    var b = btns[i];
                    var sub = (b.ButtonType ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(sub)) continue;

                    var button = new Dictionary<string, object>
                    {
                        ["type"] = "button",
                        ["sub_type"] = sub,
                        ["index"] = i.ToString()
                    };

                    if (sub == "url" && !string.IsNullOrWhiteSpace(b.TargetUrl))
                        button["parameters"] = new object[] { new { type = "text", text = b.TargetUrl! } };
                    else if (sub == "quick_reply" && !string.IsNullOrWhiteSpace(b.TargetUrl))
                        button["parameters"] = new object[] { new { type = "payload", payload = b.TargetUrl! } };

                    components.Add(button);
                }

                // full payload object for WhatsApp template
                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = dto.RecipientNumber!,
                    type = "template",
                    template = new
                    {
                        name = dto.TemplateName!,
                        language = new { code = langCode },
                        components = components
                    }
                };

                var sendResult = await SendPayloadAsync(businessId, provider, payload, dto.PhoneNumberId);

                var renderedBody = TemplateParameterHelper.FillPlaceholders(
                    dto.TemplateBody ?? "",
                    dto.TemplateParameters ?? new List<string>());

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = dto.RecipientNumber!,
                    MessageContent = dto.TemplateName!,
                    MediaUrl = dto.HeaderVideoUrl,
                    RenderedBody = renderedBody,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.ErrorMessage ?? (sendResult.Success ? null : "WhatsApp API returned an error."),
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    SentAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success ? "✅ Template sent successfully." : (sendResult.ErrorMessage ?? "❌ WhatsApp API returned an error."),
                    Data = new { Success = sendResult.Success, MessageId = sendResult.MessageId, LogId = log.Id },
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    LogId = log.Id
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await _db.MessageLogs.AddAsync(new MessageLog
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        RecipientNumber = dto.RecipientNumber ?? "",
                        MessageContent = dto.TemplateName ?? "",
                        RenderedBody = TemplateParameterHelper.FillPlaceholders(dto.TemplateBody ?? "", dto.TemplateParameters ?? new List<string>()),
                        MediaUrl = dto.HeaderVideoUrl,
                        Status = "Failed",
                        ErrorMessage = ex.Message,
                        CreatedAt = DateTime.UtcNow,
                        CTAFlowConfigId = dto.CTAFlowConfigId,
                        CTAFlowStepId = dto.CTAFlowStepId
                    });
                    await _db.SaveChangesAsync();
                }
                catch { /* ignore */ }

                return ResponseResult.ErrorInfo("❌ Template send failed", ex.Message);
            }
        }

        public async Task<ResponseResult> SendTextDirectAsync(TextMessageSendDto dto)
        {
            try
            {
                var businessId = _httpContextAccessor.HttpContext?.User?.GetBusinessId()
                    ?? throw new UnauthorizedAccessException("❌ Cannot resolve BusinessId from context.");

                // Normalize inbound intent
                string? providerUpper = string.IsNullOrWhiteSpace(dto.Provider)
                    ? null
                    : dto.Provider!.Trim().ToUpperInvariant();
                string? providerKey = providerUpper?.ToLowerInvariant();
                string? phoneNumberId = string.IsNullOrWhiteSpace(dto.PhoneNumberId) ? null : dto.PhoneNumberId!.Trim();

                // Derive provider/sender from default phone row if needed
                if (string.IsNullOrWhiteSpace(providerUpper))
                {
                    var defaultPhone = await _db.WhatsAppPhoneNumbers
                        .AsNoTracking()
                        .Where(n => n.BusinessId == businessId && n.IsActive)
                        .OrderByDescending(n => n.IsDefault)
                        .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                        .Select(n => new { n.Provider, n.PhoneNumberId })
                        .FirstOrDefaultAsync();

                    if (defaultPhone == null)
                    {
                        var anySetting = await _db.WhatsAppSettings
                            .AsNoTracking()
                            .Where(s => s.BusinessId == businessId && s.IsActive)
                            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                            .Select(s => s.Provider)
                            .FirstOrDefaultAsync();

                        if (string.IsNullOrWhiteSpace(anySetting))
                            return ResponseResult.ErrorInfo("❌ WhatsApp configuration not found (no active numbers or settings).");

                        providerUpper = anySetting.Trim().ToUpperInvariant();
                        providerKey = providerUpper.ToLowerInvariant();
                    }
                    else
                    {
                        providerUpper = (defaultPhone.Provider ?? string.Empty).Trim().ToUpperInvariant();
                        providerKey = providerUpper.ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(phoneNumberId))
                            phoneNumberId = defaultPhone.PhoneNumberId;
                    }
                }

                if (providerUpper != "PINNACLE" && providerUpper != "META_CLOUD")
                    return ResponseResult.ErrorInfo("❌ Invalid provider. Must be 'PINNACLE' or 'META_CLOUD'.");

                if (string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    var phoneRow = await _db.WhatsAppPhoneNumbers
                        .AsNoTracking()
                        .Where(n => n.BusinessId == businessId
                                    && n.IsActive
                                    && n.Provider.ToLower() == providerKey)
                        .OrderByDescending(n => n.IsDefault)
                        .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                        .Select(n => n.PhoneNumberId)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(phoneRow))
                        phoneNumberId = phoneRow;
                }

                if (providerUpper == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberId))
                    return ResponseResult.ErrorInfo("❌ Missing PhoneNumberId for META_CLOUD. Configure a default sender or pass PhoneNumberId.");

                var chosenSetting = await _db.WhatsAppSettings
                    .AsNoTracking()
                    .Where(s => s.BusinessId == businessId
                                && s.IsActive
                                && s.Provider.ToLower() == providerKey)
                    .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (chosenSetting == null)
                    return ResponseResult.ErrorInfo("❌ WhatsApp settings row not found for the selected provider.");

                // Contact upsert/touch
                Guid? contactId = null;
                var contact = await _db.Contacts.FirstOrDefaultAsync(c =>
                    c.BusinessId == businessId && c.PhoneNumber == dto.RecipientNumber);

                if (contact != null)
                {
                    contactId = contact.Id;
                    contact.LastContactedAt = DateTime.UtcNow;
                }
                else if (dto.IsSaveContact)
                {
                    contact = new Contact
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Name = "WhatsApp User",
                        PhoneNumber = dto.RecipientNumber,
                        CreatedAt = DateTime.UtcNow,
                        LastContactedAt = DateTime.UtcNow
                    };
                    _db.Contacts.Add(contact);
                    contactId = contact.Id;
                }

                await _db.SaveChangesAsync();

                // Send
                var sendResult = await SendViaProviderAsync(
                    businessId,
                    providerUpper!,
                    p => p.SendTextAsync(dto.RecipientNumber, dto.TextContent),
                    phoneNumberId
                );

                // Extract provider message id if missing
                string? messageId = sendResult.MessageId;
                if (string.IsNullOrWhiteSpace(messageId) && !string.IsNullOrWhiteSpace(sendResult.RawResponse))
                {
                    try
                    {
                        var raw = sendResult.RawResponse!.TrimStart();
                        if (raw.StartsWith("{"))
                        {
                            using var parsed = JsonDocument.Parse(raw);
                            if (parsed.RootElement.TryGetProperty("messages", out var msgs)
                                && msgs.ValueKind == JsonValueKind.Array
                                && msgs.GetArrayLength() > 0
                                && msgs[0].TryGetProperty("id", out var idProp))
                            {
                                messageId = idProp.GetString();
                            }
                        }
                    }
                    catch { /* ignore parse issues */ }
                }

                // Log
                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TextContent,
                    RenderedBody = dto.TextContent,
                    ContactId = contactId,
                    MediaUrl = null,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    MessageId = messageId
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                // Optional campaign mapping
                Guid? campaignSendLogId = null;
                if (dto.Source == "campaign" && !string.IsNullOrEmpty(messageId))
                {
                    try { campaignSendLogId = await _messageIdResolver.ResolveCampaignSendLogIdAsync(messageId); }
                    catch { /* non-fatal */ }
                }

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success
                        ? "✅ Text message sent successfully."
                        : (sendResult.Message ?? "❌ WhatsApp API returned an error."),
                    Data = new
                    {
                        Success = sendResult.Success,
                        MessageId = messageId,
                        LogId = log.Id,
                        CampaignSendLogId = campaignSendLogId
                    },
                    RawResponse = sendResult.RawResponse,
                    MessageId = messageId,
                    LogId = log.Id
                };
            }
            catch (Exception ex)
            {
                try
                {
                    var businessId = _httpContextAccessor.HttpContext?.User?.GetBusinessId();
                    if (businessId != null)
                    {
                        await _db.MessageLogs.AddAsync(new MessageLog
                        {
                            Id = Guid.NewGuid(),
                            BusinessId = businessId.Value,
                            RecipientNumber = dto.RecipientNumber,
                            MessageContent = dto.TextContent,
                            Status = "Failed",
                            ErrorMessage = ex.Message,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _db.SaveChangesAsync();
                    }
                }
                catch { /* ignore */ }

                return ResponseResult.ErrorInfo("❌ Failed to send text message.", ex.ToString());
            }
        }

        public async Task<ResponseResult> SendAutomationReply(TextMessageSendDto dto)
        {
            try
            {
                var businessId =
                    dto.BusinessId != Guid.Empty
                        ? dto.BusinessId
                        : _httpContextAccessor.HttpContext?.User?.GetBusinessId()
                          ?? throw new UnauthorizedAccessException("❌ Cannot resolve BusinessId from context or DTO.");

                if (string.IsNullOrWhiteSpace(dto.Provider) ||
                    (dto.Provider != "PINNACLE" && dto.Provider != "META_CLOUD"))
                {
                    return ResponseResult.ErrorInfo("❌ Invalid provider.",
                        "Provider must be exactly 'PINNACLE' or 'META_CLOUD'.");
                }

                Guid? contactId = null;
                try
                {
                    var contact = await _contactService.FindOrCreateAsync(businessId, dto.RecipientNumber);
                    contactId = contact.Id;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to resolve or create contact: {ex.Message}");
                }

                var sendResult = await SendViaProviderAsync(
                    businessId,
                    dto.Provider,
                    p => p.SendTextAsync(dto.RecipientNumber, dto.TextContent),
                    dto.PhoneNumberId
                );

                string? messageId = sendResult.MessageId;
                var raw = sendResult.RawResponse;
                if (string.IsNullOrWhiteSpace(messageId) && !string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        var s = raw.TrimStart();
                        if (s.StartsWith("{"))
                        {
                            using var parsed = JsonDocument.Parse(s);
                            if (parsed.RootElement.TryGetProperty("messages", out var messages) &&
                                messages.ValueKind == JsonValueKind.Array &&
                                messages.GetArrayLength() > 0 &&
                                messages[0].TryGetProperty("id", out var idProp))
                            {
                                messageId = idProp.GetString();
                            }
                        }
                    }
                    catch { /* ignore */ }
                }

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TextContent,
                    RenderedBody = dto.TextContent,
                    ContactId = contactId,
                    MediaUrl = null,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    MessageId = messageId
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                Guid? campaignSendLogId = null;
                if (dto.Source == "campaign" && !string.IsNullOrEmpty(messageId))
                {
                    try
                    {
                        campaignSendLogId = await _messageIdResolver.ResolveCampaignSendLogIdAsync(messageId);
                        Console.WriteLine($"📦 CampaignSendLog resolved: {campaignSendLogId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Failed to resolve campaign log for {messageId}: {ex.Message}");
                    }
                }

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success
                        ? "✅ Text message sent successfully."
                        : (sendResult.Message ?? "❌ WhatsApp API returned an error."),
                    Data = new
                    {
                        Success = sendResult.Success,
                        MessageId = messageId,
                        LogId = log.Id,
                        CampaignSendLogId = campaignSendLogId
                    },
                    RawResponse = sendResult.RawResponse,
                    MessageId = messageId,
                    LogId = log.Id
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in SendAutomationReply: {ex.Message}");

                try
                {
                    var businessId =
                        dto.BusinessId != Guid.Empty
                            ? dto.BusinessId
                            : _httpContextAccessor.HttpContext?.User?.GetBusinessId()
                              ?? throw new UnauthorizedAccessException("❌ Cannot resolve BusinessId in failure path.");

                    await _db.MessageLogs.AddAsync(new MessageLog
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        RecipientNumber = dto.RecipientNumber,
                        MessageContent = dto.TextContent,
                        Status = "Failed",
                        ErrorMessage = ex.Message,
                        CreatedAt = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync();
                }
                catch { /* ignore log errors */ }

                return ResponseResult.ErrorInfo("❌ Failed to send text message.", ex.ToString());
            }
        }

        /// <summary>
        /// Sends a simple text auto-reply on behalf of a business.
        /// This helper assumes Meta Cloud as the default provider and
        /// uses the default active WhatsAppPhoneNumber to resolve PhoneNumberId.
        /// Intended for AutoReplyBuilder / webhook runtime (no user context).
        /// </summary>
        public async Task<ResponseResult> SendAutoReplyTextAsync(
       Guid businessId,
       string recipientNumber,
       string body,
       CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
            {
                return ResponseResult.ErrorInfo(
                    "❌ Auto-reply failed.",
                    "BusinessId is required.");
            }

            if (string.IsNullOrWhiteSpace(recipientNumber))
            {
                return ResponseResult.ErrorInfo(
                    "❌ Auto-reply failed.",
                    "Recipient phone number is required.");
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return ResponseResult.ErrorInfo(
                    "❌ Auto-reply failed.",
                    "Reply body is empty.");
            }

            try
            {
                // 1) Normalize the recipient a bit (full E.164 normalization happens elsewhere)
                var trimmedNumber = recipientNumber.Trim();

                // 2) Load active WhatsApp settings for this business
                var setting = await _db.WhatsAppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.IsActive, ct);

                if (setting == null)
                {
                    _logger.LogWarning(
                        "⚠️ AutoReply: WhatsApp settings not found for BusinessId={BusinessId}.",
                        businessId);

                    return ResponseResult.ErrorInfo(
                        "❌ Auto-reply failed.",
                        "WhatsApp settings are not configured for this business.");
                }

                var provider = (setting.Provider ?? string.Empty)
                    .Trim()
                    .ToUpperInvariant();

                if (provider != "META_CLOUD" && provider != "PINNACLE")
                {
                    _logger.LogWarning(
                        "⚠️ AutoReply: Unsupported provider '{Provider}' for BusinessId={BusinessId}.",
                        provider,
                        businessId);

                    return ResponseResult.ErrorInfo(
                        "❌ Auto-reply failed.",
                        "WhatsApp provider is not correctly configured for this business.");
                }

                // 3) Resolve the default sender (PhoneNumberId) for this provider
                var phone = await _db.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(p => p.BusinessId == businessId
                                && p.IsActive
                                && p.Provider.ToLower() == provider.ToLower())
                    .OrderByDescending(p => p.IsDefault)
                    .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                    .Select(p => new { p.PhoneNumberId, p.WhatsAppBusinessNumber })
                    .FirstOrDefaultAsync(ct);

                string? phoneNumberId = phone?.PhoneNumberId;

                if (provider == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    _logger.LogWarning(
                        "⚠️ AutoReply: No default PhoneNumberId configured for BusinessId={BusinessId}, Provider={Provider}.",
                        businessId,
                        provider);

                    return ResponseResult.ErrorInfo(
                        "❌ Auto-reply failed.",
                        "No default WhatsApp sender number is configured for this business.");
                }

                // 4) Build DTO for the core text sender
                var dto = new TextMessageSendDto
                {
                    BusinessId = businessId,
                    RecipientNumber = trimmedNumber,
                    TextContent = body,
                    Provider = provider,         // use provider from settings
                    PhoneNumberId = phoneNumberId,
                    Source = "auto-reply"
                };

                _logger.LogInformation(
                    "📤 AutoReply: sending simple text reply for BusinessId={BusinessId}, Recipient={Recipient}, Preview={Preview}",
                    businessId,
                    trimmedNumber,
                    body.Length > 60 ? body.Substring(0, 60) + "..." : body);

                // 5) Delegate to the existing pipeline (logs + OutboundMessageJob, etc.)
                var result = await SendAutomationReply(dto);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "❌ AutoReply: SendAutomationReply failed for BusinessId={BusinessId}, Recipient={Recipient}. Error={Error}",
                        businessId,
                        trimmedNumber,
                        result.Message);
                }
                else
                {
                    _logger.LogInformation(
                        "✅ AutoReply: message sent successfully for BusinessId={BusinessId}, Recipient={Recipient}.",
                        businessId,
                        trimmedNumber);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "❌ AutoReply: unexpected exception while sending simple text reply for BusinessId={BusinessId}, Recipient={Recipient}.",
                    businessId,
                    recipientNumber);

                return ResponseResult.ErrorInfo(
                    "❌ Auto-reply failed due to an unexpected error.",
                    ex.Message);
            }
        }


        /// <summary>
        /// New overload for AutoReply that accepts a DeliveryMode.
        /// Currently it delegates to the legacy implementation so
        /// behaviour is unchanged. In the next steps we will route
        /// Immediate vs Queued differently.
        /// </summary>
        public Task<ResponseResult> SendAutoReplyTextAsync(
            Guid businessId,
            string recipientNumber,
            string body,
            DeliveryMode mode,
            CancellationToken ct = default)
        {
            // 🔁 Step 1: ignore mode, keep current behaviour.
            return SendAutoReplyTextAsync(businessId, recipientNumber, body, ct);
        }



        #region SendTemplateMessageSimpleAsync Overload



        //public async Task<ResponseResult> SendTemplateMessageSimpleAsync(Guid businessId, SimpleTemplateMessageDto dto)
        //{
        //    try
        //    {
        //        // Normalize inbound
        //        string? providerUpper = string.IsNullOrWhiteSpace(dto.Provider)
        //            ? null
        //            : dto.Provider!.Trim().ToUpperInvariant();
        //        string? providerKey = providerUpper?.ToLowerInvariant();
        //        string? phoneNumberId = string.IsNullOrWhiteSpace(dto.PhoneNumberId)
        //            ? null
        //            : dto.PhoneNumberId!.Trim();

        //        // Resolve missing provider/sender from WhatsAppPhoneNumbers
        //        if (string.IsNullOrWhiteSpace(providerUpper))
        //        {
        //            var defPhone = await _db.WhatsAppPhoneNumbers
        //                .AsNoTracking()
        //                .Where(n => n.BusinessId == businessId && n.IsActive)
        //                .OrderByDescending(n => n.IsDefault)
        //                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
        //                .Select(n => new { n.Provider, n.PhoneNumberId })
        //                .FirstOrDefaultAsync();

        //            if (defPhone != null)
        //            {
        //                providerUpper = (defPhone.Provider ?? string.Empty).Trim().ToUpperInvariant();
        //                providerKey = providerUpper.ToLowerInvariant();
        //                if (string.IsNullOrWhiteSpace(phoneNumberId))
        //                    phoneNumberId = defPhone.PhoneNumberId;
        //            }
        //        }

        //        if (string.IsNullOrWhiteSpace(providerUpper))
        //        {
        //            var anySettingProvider = await _db.WhatsAppSettings
        //                .AsNoTracking()
        //                .Where(s => s.BusinessId == businessId && s.IsActive)
        //                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
        //                .Select(s => s.Provider)
        //                .FirstOrDefaultAsync();

        //            if (!string.IsNullOrWhiteSpace(anySettingProvider))
        //            {
        //                providerUpper = anySettingProvider.Trim().ToUpperInvariant();
        //                providerKey = providerUpper.ToLowerInvariant();
        //            }
        //        }

        //        if (providerUpper != "PINNACLE" && providerUpper != "META_CLOUD")
        //        {
        //            return ResponseResult.ErrorInfo(
        //                "❌ Missing provider.",
        //                "No active WhatsApp sender found. Configure a PINNACLE or META_CLOUD sender for this business."
        //            );
        //        }

        //        if (string.IsNullOrWhiteSpace(phoneNumberId))
        //        {
        //            var pn = await _db.WhatsAppPhoneNumbers
        //                .AsNoTracking()
        //                .Where(n => n.BusinessId == businessId
        //                            && n.IsActive
        //                            && n.Provider.ToLower() == providerKey)
        //                .OrderByDescending(n => n.IsDefault)
        //                .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
        //                .Select(n => n.PhoneNumberId)
        //                .FirstOrDefaultAsync();

        //            if (!string.IsNullOrWhiteSpace(pn))
        //                phoneNumberId = pn;
        //        }

        //        if (providerUpper == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberId))
        //            return ResponseResult.ErrorInfo("❌ Missing PhoneNumberId for META_CLOUD. Configure a default sender or pass PhoneNumberId.");

        //        // Build minimal components (body only)
        //        var parameters = (dto.TemplateParameters ?? new List<string>())
        //            .Select(p => new { type = "text", text = p })
        //            .ToArray();

        //        var components = new List<object>();
        //        if (parameters.Length > 0)
        //            components.Add(new { type = "body", parameters });

        //        var lang = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode!;
        //        _logger?.LogInformation("➡️ SEND-INTENT tmpl={Template} to={To} provider={Provider} pnid={PhoneNumberId}",
        //            dto.TemplateName, dto.RecipientNumber, providerUpper, phoneNumberId ?? "(default)");

        //        var sendResult = await SendViaProviderAsync(
        //            businessId,
        //            providerUpper,
        //            p => p.SendTemplateAsync(dto.RecipientNumber, dto.TemplateName, lang, components),
        //            phoneNumberId
        //        );

        //        var log = new MessageLog
        //        {
        //            Id = Guid.NewGuid(),
        //            BusinessId = businessId,
        //            RecipientNumber = dto.RecipientNumber,
        //            MessageContent = dto.TemplateName,
        //            RenderedBody = TemplateParameterHelper.FillPlaceholders(
        //                dto.TemplateBody ?? string.Empty,
        //                dto.TemplateParameters ?? new List<string>()),

        //            CTAFlowConfigId = dto.CTAFlowConfigId,
        //            CTAFlowStepId = dto.CTAFlowStepId,

        //            Provider = providerUpper,
        //            ProviderMessageId = sendResult.MessageId,

        //            Status = sendResult.Success ? "Sent" : "Failed",
        //            ErrorMessage = sendResult.Success ? null : sendResult.Message,
        //            RawResponse = sendResult.RawResponse,
        //            MessageId = sendResult.MessageId,
        //            SentAt = sendResult.Success ? DateTime.UtcNow : (DateTime?)null,
        //            CreatedAt = DateTime.UtcNow,
        //            Source = "api"
        //        };

        //        await _db.MessageLogs.AddAsync(log);
        //        await _db.SaveChangesAsync();

        //        return new ResponseResult
        //        {
        //            Success = sendResult.Success,
        //            Message = sendResult.Success
        //                ? "✅ Template sent successfully."
        //                : (sendResult.Message ?? "❌ WhatsApp API returned an error."),
        //            Data = new
        //            {
        //                Success = sendResult.Success,
        //                MessageId = sendResult.MessageId,
        //                LogId = log.Id
        //            },
        //            RawResponse = sendResult.RawResponse,
        //            MessageId = sendResult.MessageId,
        //            LogId = log.Id
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        try
        //        {
        //            await _db.MessageLogs.AddAsync(new MessageLog
        //            {
        //                Id = Guid.NewGuid(),
        //                BusinessId = businessId,
        //                RecipientNumber = dto.RecipientNumber,
        //                MessageContent = dto.TemplateName,
        //                RenderedBody = TemplateParameterHelper.FillPlaceholders(
        //                    dto.TemplateBody ?? string.Empty,
        //                    dto.TemplateParameters ?? new List<string>()),
        //                Status = "Failed",
        //                ErrorMessage = ex.Message,
        //                CreatedAt = DateTime.UtcNow,
        //                Source = "api"
        //            });
        //            await _db.SaveChangesAsync();
        //        }
        //        catch { /* ignore */ }

        //        return ResponseResult.ErrorInfo("❌ Template send failed", ex.Message);
        //    }
        //}
        ///// <summary>
        ///// New overload that is aware of DeliveryMode.
        ///// For now, it simply delegates to the existing implementation
        ///// (which behaves as immediate/direct send).
        ///// In the next steps, we will branch on <paramref name="mode"/>.
        ///// </summary>
        //// Over Load method
        //public Task<ResponseResult> SendTemplateMessageSimpleAsync(
        //    Guid businessId,
        //    SimpleTemplateMessageDto dto,
        //    DeliveryMode mode)
        //{
        //    // 🔁 Step 1: keep behaviour identical.
        //    // We ignore `mode` for now and just call the existing method.
        //    // Later we will:
        //    //  - use Queued mode for outbox
        //    //  - use Immediate mode for direct Meta Cloud sends
        //    return SendTemplateMessageSimpleAsync(businessId, dto);
        //}

        #endregion

        public async Task<ResponseResult> SendTemplateMessageSimpleAsync(Guid businessId, SimpleTemplateMessageDto dto)
        {
            try
            {
                // 🔎 Normalize inbound + respect DeliveryMode for logging/analytics
                var mode = dto.DeliveryMode; // default is Queued if caller didn't set

                string? providerUpper = string.IsNullOrWhiteSpace(dto.Provider)
                    ? null
                    : dto.Provider!.Trim().ToUpperInvariant();
                string? providerKey = providerUpper?.ToLowerInvariant();
                string? phoneNumberId = string.IsNullOrWhiteSpace(dto.PhoneNumberId)
                    ? null
                    : dto.PhoneNumberId!.Trim();

                // Resolve missing provider/sender from WhatsAppPhoneNumbers
                if (string.IsNullOrWhiteSpace(providerUpper))
                {
                    var defPhone = await _db.WhatsAppPhoneNumbers
                        .AsNoTracking()
                        .Where(n => n.BusinessId == businessId && n.IsActive)
                        .OrderByDescending(n => n.IsDefault)
                        .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                        .Select(n => new { n.Provider, n.PhoneNumberId })
                        .FirstOrDefaultAsync();

                    if (defPhone != null)
                    {
                        providerUpper = (defPhone.Provider ?? string.Empty).Trim().ToUpperInvariant();
                        providerKey = providerUpper.ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(phoneNumberId))
                            phoneNumberId = defPhone.PhoneNumberId;
                    }
                }

                if (string.IsNullOrWhiteSpace(providerUpper))
                {
                    var anySettingProvider = await _db.WhatsAppSettings
                        .AsNoTracking()
                        .Where(s => s.BusinessId == businessId && s.IsActive)
                        .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                        .Select(s => s.Provider)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(anySettingProvider))
                    {
                        providerUpper = anySettingProvider.Trim().ToUpperInvariant();
                        providerKey = providerUpper.ToLowerInvariant();
                    }
                }

                if (providerUpper != "PINNACLE" && providerUpper != "META_CLOUD")
                {
                    return ResponseResult.ErrorInfo(
                        "❌ Missing provider.",
                        "No active WhatsApp sender found. Configure a PINNACLE or META_CLOUD sender for this business."
                    );
                }

                if (string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    var pn = await _db.WhatsAppPhoneNumbers
                        .AsNoTracking()
                        .Where(n => n.BusinessId == businessId
                                    && n.IsActive
                                    && n.Provider.ToLower() == providerKey)
                        .OrderByDescending(n => n.IsDefault)
                        .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                        .Select(n => n.PhoneNumberId)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(pn))
                        phoneNumberId = pn;
                }

                if (providerUpper == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberId))
                    return ResponseResult.ErrorInfo(
                        "❌ Missing PhoneNumberId for META_CLOUD. Configure a default sender or pass PhoneNumberId.");

                // Build minimal components (body only)
                var parameters = (dto.TemplateParameters ?? new List<string>())
                    .Select(p => new { type = "text", text = p })
                    .ToArray();

                var components = new List<object>();
                if (parameters.Length > 0)
                    components.Add(new { type = "body", parameters });

                var lang = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode!;

                _logger?.LogInformation(
                    "➡️ SEND-INTENT tmpl={Template} to={To} provider={Provider} pnid={PhoneNumberId} mode={Mode} ctaFlowConfig={CtaFlowConfigId} ctaFlowStep={CtaFlowStepId}",
                    dto.TemplateName,
                    dto.RecipientNumber,
                    providerUpper,
                    phoneNumberId ?? "(default)",
                    mode,
                    dto.CTAFlowConfigId,
                    dto.CTAFlowStepId
                );

                // 🧵 IMPORTANT:
                // For now, BOTH Queued + Immediate behave the same: direct send.
                // Later, if you wire a true Outbox, you can special-case `mode == DeliveryMode.Queued`
                // to enqueue instead of calling SendViaProviderAsync.
                var sendResult = await SendViaProviderAsync(
                    businessId,
                    providerUpper,
                    p => p.SendTemplateAsync(dto.RecipientNumber, dto.TemplateName, lang, components),
                    phoneNumberId
                );

                // 🏷 Smarter Source tagging:
                // - If CTAFlowConfigId is set → this is a CTA Flow step
                // - Else → normal API/template send
                var isCtaFlow = dto.CTAFlowConfigId.HasValue;

                var sourceTag = isCtaFlow
                    ? (mode == DeliveryMode.Immediate ? "cta-flow-immediate" : "cta-flow-queued")
                    : (mode == DeliveryMode.Immediate ? "api-immediate" : "api-queued");

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TemplateName,
                    RenderedBody = TemplateParameterHelper.FillPlaceholders(
                        dto.TemplateBody ?? string.Empty,
                        dto.TemplateParameters ?? new List<string>()),

                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId,

                    Provider = providerUpper,
                    ProviderMessageId = sendResult.MessageId,

                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    SentAt = sendResult.Success ? DateTime.UtcNow : (DateTime?)null,
                    CreatedAt = DateTime.UtcNow,

                    // 👇 now carries CTA vs non-CTA + mode
                    Source = sourceTag
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success
                        ? "✅ Template sent successfully."
                        : (sendResult.Message ?? "❌ WhatsApp API returned an error."),
                    Data = new
                    {
                        Success = sendResult.Success,
                        MessageId = sendResult.MessageId,
                        LogId = log.Id
                    },
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    LogId = log.Id
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await _db.MessageLogs.AddAsync(new MessageLog
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        RecipientNumber = dto.RecipientNumber,
                        MessageContent = dto.TemplateName,
                        RenderedBody = TemplateParameterHelper.FillPlaceholders(
                            dto.TemplateBody ?? string.Empty,
                            dto.TemplateParameters ?? new List<string>()),
                        Status = "Failed",
                        ErrorMessage = ex.Message,
                        CreatedAt = DateTime.UtcNow,
                        Source = "api-error"
                    });
                    await _db.SaveChangesAsync();
                }
                catch { /* ignore */ }

                return ResponseResult.ErrorInfo("❌ Template send failed", ex.Message);
            }
        }

        /// <summary>
        /// New overload that is aware of DeliveryMode.
        /// Right now it just stamps the mode into the DTO
        /// and calls the core implementation. Later, if you
        /// implement a real outbox, this is the natural place
        /// to branch behaviour.
        /// </summary>
        public Task<ResponseResult> SendTemplateMessageSimpleAsync(
            Guid businessId,
            SimpleTemplateMessageDto dto,
            DeliveryMode mode)
        {
            // Keep DTO + intent in sync
            dto.DeliveryMode = mode;

            // For now, both modes use the same path.
            return SendTemplateMessageSimpleAsync(businessId, dto);
        }

        public async Task<ResponseResult> SendImageCampaignAsync(Guid campaignId, Guid businessId, string sentBy)
        {
            try
            {
                var campaign = await _db.Campaigns
                    .Include(c => c.MultiButtons)
                    .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId);

                if (campaign == null)
                    return ResponseResult.ErrorInfo("❌ Campaign not found or unauthorized.");

                var recipients = await _db.CampaignRecipients
                    .Include(r => r.Contact)
                    .Where(r => r.CampaignId == campaignId && r.BusinessId == businessId)
                    .ToListAsync();

                if (recipients.Count == 0)
                    return ResponseResult.ErrorInfo("⚠️ No recipients assigned to this campaign.");

                var validButtons = campaign.MultiButtons
                    ?.Where(b => !string.IsNullOrWhiteSpace(b.Title))
                    .Select(b => new CtaButtonDto { Title = b.Title, Value = b.Value })
                    .ToList();

                if (validButtons == null || validButtons.Count == 0)
                    return ResponseResult.ErrorInfo("❌ At least one CTA button with a valid title is required.");

                int successCount = 0, failCount = 0;

                foreach (var recipient in recipients)
                {
                    if (recipient.Contact == null || string.IsNullOrWhiteSpace(recipient.Contact.PhoneNumber))
                    {
                        recipient.Status = "Failed";
                        recipient.UpdatedAt = DateTime.UtcNow;
                        failCount++;
                        continue;
                    }

                    var dto = new SendMessageDto
                    {
                        BusinessId = businessId,
                        RecipientNumber = recipient.Contact.PhoneNumber,
                        MessageType = MessageTypeEnum.Image,
                        MediaUrl = campaign.ImageUrl,
                        TextContent = campaign.MessageTemplate,
                        CtaButtons = validButtons,

                        CampaignId = campaign.Id,
                        SourceModule = "image-campaign",
                        CustomerId = recipient.Contact.Id.ToString(),
                        CustomerName = recipient.Contact.Name,
                        CustomerPhone = recipient.Contact.PhoneNumber,
                        CTATriggeredFrom = "campaign"
                    };

                    var result = await SendImageWithCtaAsync(dto);

                    var sendLog = new CampaignSendLog
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = campaign.Id,
                        ContactId = recipient.Contact.Id,
                        RecipientId = recipient.Id,
                        MessageLogId = result?.LogId,
                        SendStatus = result.Success ? "Sent" : "Failed",
                        SentAt = DateTime.UtcNow,
                        CreatedBy = sentBy,
                        BusinessId = businessId,
                    };
                    await _db.CampaignSendLogs.AddAsync(sendLog);

                    if (result.Success)
                    {
                        recipient.Status = "Sent";
                        recipient.SentAt = DateTime.UtcNow;
                        recipient.UpdatedAt = DateTime.UtcNow;
                        successCount++;
                    }
                    else
                    {
                        recipient.Status = "Failed";
                        recipient.UpdatedAt = DateTime.UtcNow;
                        failCount++;
                    }
                }

                await _db.SaveChangesAsync();

                await _db.Campaigns
                    .Where(c => c.Id == campaign.Id && c.BusinessId == businessId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Status, _ => "Sent")
                        .SetProperty(c => c.UpdatedAt, _ => DateTime.UtcNow));

                return ResponseResult.SuccessInfo($"✅ Campaign sent.\n📤 Success: {successCount}, ❌ Failed: {failCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending image campaign: {ex.Message}");
                return ResponseResult.ErrorInfo("❌ Unexpected error while sending image campaign.", ex.ToString());
            }
        }

        public async Task<ResponseResult> SendTemplateCampaignAsync(Guid campaignId, Guid businessId, string sentBy)
        {
            try
            {
                var campaign = await _db.Campaigns
                    .AsNoTracking()
                    .Where(c => c.Id == campaignId && c.BusinessId == businessId)
                    .Select(c => new
                    {
                        c.Id,
                        c.BusinessId,
                        c.MessageTemplate,
                        c.TemplateId,
                        c.Provider,
                        c.PhoneNumberId,
                        c.ImageUrl
                    })
                    .FirstOrDefaultAsync();

                if (campaign == null)
                    return ResponseResult.ErrorInfo("❌ Campaign not found or unauthorized.");

                var templateName = !string.IsNullOrWhiteSpace(campaign.TemplateId)
                    ? campaign.TemplateId!
                    : (campaign.MessageTemplate ?? "").Trim();

                if (string.IsNullOrWhiteSpace(templateName))
                    return ResponseResult.ErrorInfo("❌ Campaign has no template selected.");

                var lang = await _db.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(w => w.BusinessId == businessId && w.Name == templateName)
                    .OrderByDescending(w => (w.UpdatedAt > w.CreatedAt ? w.UpdatedAt : w.CreatedAt))
                    .Select(w => w.LanguageCode)
                    .FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(lang)) lang = "en_US";

                var recipients = await _db.CampaignRecipients
                    .AsNoTracking()
                    .Include(r => r.AudienceMember)
                    .Include(r => r.Contact)
                    .Where(r => r.CampaignId == campaignId && r.BusinessId == businessId)
                    .Select(r => new
                    {
                        r.Id,
                        r.ContactId,
                        AudienceContactId = r.AudienceMember != null ? r.AudienceMember.ContactId : (Guid?)null,
                        r.ResolvedParametersJson,
                        r.ResolvedButtonUrlsJson,
                        Phone = r.AudienceMember != null && !string.IsNullOrEmpty(r.AudienceMember.PhoneE164)
                                ? r.AudienceMember.PhoneE164
                                : (r.Contact != null ? r.Contact.PhoneNumber : null)
                    })
                    .ToListAsync();

                if (recipients.Count == 0)
                    return ResponseResult.ErrorInfo("⚠️ No recipients materialized for this campaign.");

                var provider = (campaign.Provider ?? "").Trim().ToUpperInvariant();
                if (provider != "PINNACLE" && provider != "META_CLOUD")
                    return ResponseResult.ErrorInfo("❌ Invalid provider on campaign. Must be 'PINNACLE' or 'META_CLOUD'.");

                var phoneNumberId = string.IsNullOrWhiteSpace(campaign.PhoneNumberId) ? null : campaign.PhoneNumberId!.Trim();
                if (string.IsNullOrWhiteSpace(phoneNumberId))
                    return ResponseResult.ErrorInfo("❌ Campaign has no sender number (PhoneNumberId).");

                int success = 0, fail = 0;
                var successIds = new List<Guid>(recipients.Count);
                var failedIds = new List<Guid>();
                var sendLogs = new List<CampaignSendLog>(recipients.Count);

                foreach (var r in recipients)
                {
                    if (string.IsNullOrWhiteSpace(r.Phone))
                    {
                        failedIds.Add(r.Id);
                        continue;
                    }

                    string[] bodyParams;
                    try
                    {
                        bodyParams = string.IsNullOrWhiteSpace(r.ResolvedParametersJson)
                            ? Array.Empty<string>()
                            : JsonSerializer.Deserialize<string[]>(r.ResolvedParametersJson!) ?? Array.Empty<string>();
                    }
                    catch { bodyParams = Array.Empty<string>(); }

                    Dictionary<string, string> buttonVars;
                    try
                    {
                        buttonVars = string.IsNullOrWhiteSpace(r.ResolvedButtonUrlsJson)
                            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            : JsonSerializer.Deserialize<Dictionary<string, string>>(r.ResolvedButtonUrlsJson!)
                              ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    catch { buttonVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }

                    var components = new List<object>();

                    var headerImage = !string.IsNullOrWhiteSpace(campaign.ImageUrl)
                        ? campaign.ImageUrl
                        : (buttonVars.TryGetValue("header.image_url", out var hv) && !string.IsNullOrWhiteSpace(hv) ? hv : null);

                    if (!string.IsNullOrWhiteSpace(headerImage))
                    {
                        components.Add(new
                        {
                            type = "header",
                            parameters = new object[]
                            {
                                new { type = "image", image = new { link = headerImage! } }
                            }
                        });
                    }

                    if (bodyParams.Length > 0)
                    {
                        components.Add(new
                        {
                            type = "body",
                            parameters = bodyParams.Select(p => (object)new { type = "text", text = p }).ToArray()
                        });
                    }

                    foreach (var pos in new[] { 1, 2, 3 })
                    {
                        var key = $"button{pos}.url_param";
                        if (buttonVars.TryGetValue(key, out var urlParam) && !string.IsNullOrWhiteSpace(urlParam))
                        {
                            components.Add(new
                            {
                                type = "button",
                                sub_type = "url",
                                index = (pos - 1).ToString(),
                                parameters = new object[] { new { type = "text", text = urlParam } }
                            });
                        }
                    }

                    var payload = new
                    {
                        messaging_product = "whatsapp",
                        to = r.Phone!,
                        type = "template",
                        template = new
                        {
                            name = templateName,
                            language = new { code = lang },
                            components = components
                        }
                    };

                    var result = await SendPayloadAsync(businessId, provider, payload, phoneNumberId);
                    if (result.Success) { success++; successIds.Add(r.Id); } else { fail++; failedIds.Add(r.Id); }

                    var contactId = r.ContactId ?? r.AudienceContactId;
                    sendLogs.Add(new CampaignSendLog
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = campaign.Id,
                        ContactId = contactId,
                        RecipientId = r.Id,
                        MessageLogId = result?.LogId,
                        SendStatus = result.Success ? "Sent" : "Failed",
                        SentAt = DateTime.UtcNow,
                        CreatedBy = sentBy,
                        BusinessId = businessId,
                    });
                }

                if (sendLogs.Count > 0)
                    await _db.CampaignSendLogs.AddRangeAsync(sendLogs);

                if (successIds.Count > 0)
                {
                    await _db.CampaignRecipients
                        .Where(x => x.CampaignId == campaignId && x.BusinessId == businessId && successIds.Contains(x.Id))
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.Status, _ => "Sent")
                            .SetProperty(x => x.SentAt, _ => DateTime.UtcNow)
                            .SetProperty(x => x.UpdatedAt, _ => DateTime.UtcNow));
                }

                if (failedIds.Count > 0)
                {
                    await _db.CampaignRecipients
                        .Where(x => x.CampaignId == campaignId && x.BusinessId == businessId && failedIds.Contains(x.Id))
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.Status, _ => "Failed")
                            .SetProperty(x => x.UpdatedAt, _ => DateTime.UtcNow));
                }

                await _db.SaveChangesAsync();

                await _db.Campaigns
                    .Where(c => c.Id == campaign.Id && c.BusinessId == businessId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Status, _ => "Sent")
                        .SetProperty(c => c.UpdatedAt, _ => DateTime.UtcNow));

                return ResponseResult.SuccessInfo($"✅ Template campaign sent. 📤 Success: {success}, ❌ Failed: {fail}");
            }
            catch (Exception ex)
            {
                return ResponseResult.ErrorInfo("❌ Error sending template campaign.", ex.ToString());
            }
        }

        public async Task<ResponseResult> SendImageWithCtaAsync(SendMessageDto dto)
        {
            try
            {
                Console.WriteLine($"📤 Sending image+CTA to {dto.RecipientNumber}");

                if (string.IsNullOrWhiteSpace(dto.TextContent))
                    return ResponseResult.ErrorInfo("❌ Image message caption (TextContent) cannot be empty.");

                if (string.IsNullOrWhiteSpace(dto.Provider) ||
                    (dto.Provider != "PINNACLE" && dto.Provider != "META_CLOUD"))
                {
                    return ResponseResult.ErrorInfo("❌ Invalid provider.",
                        "Provider must be exactly 'PINNACLE' or 'META_CLOUD'.");
                }

                var validButtons = dto.CtaButtons?
                    .Where(b => !string.IsNullOrWhiteSpace(b.Title))
                    .Take(3)
                    .Select((btn, index) => new
                    {
                        type = "reply",
                        reply = new
                        {
                            id = $"btn_{index + 1}_{Guid.NewGuid():N}".Substring(0, 16),
                            title = btn.Title
                        }
                    })
                    .ToList();

                if (validButtons == null || validButtons.Count == 0)
                    return ResponseResult.ErrorInfo("❌ At least one CTA button with a valid title is required.");

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = dto.RecipientNumber,
                    type = "interactive",
                    interactive = new
                    {
                        type = "button",
                        body = new { text = dto.TextContent },
                        action = new { buttons = validButtons }
                    },
                    image = string.IsNullOrWhiteSpace(dto.MediaUrl) ? null : new { link = dto.MediaUrl }
                };

                var sendResult = await SendViaProviderAsync(
                    dto.BusinessId,
                    dto.Provider,
                    p => p.SendInteractiveAsync(payload),
                    dto.PhoneNumberId
                );

                string? messageId = sendResult.MessageId;
                if (string.IsNullOrWhiteSpace(messageId) && !string.IsNullOrWhiteSpace(sendResult.RawResponse))
                {
                    try
                    {
                        var raw = sendResult.RawResponse.TrimStart();
                        if (raw.StartsWith("{"))
                        {
                            using var doc = JsonDocument.Parse(raw);
                            if (doc.RootElement.TryGetProperty("messages", out var msgs) &&
                                msgs.ValueKind == JsonValueKind.Array &&
                                msgs.GetArrayLength() > 0 &&
                                msgs[0].TryGetProperty("id", out var idProp))
                            {
                                messageId = idProp.GetString();
                            }
                        }
                    }
                    catch { /* best-effort */ }
                }

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TextContent ?? "[Image with CTA]",
                    RenderedBody = dto.TextContent ?? "",
                    MediaUrl = dto.MediaUrl,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    MessageId = messageId,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId,
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success
                        ? "✅ Image+CTA message sent."
                        : (sendResult.Message ?? "❌ WhatsApp API returned an error."),
                    Data = new
                    {
                        Success = sendResult.Success,
                        MessageId = messageId,
                        LogId = log.Id

                    },
                    RawResponse = sendResult.RawResponse,
                    MessageId = messageId,
                    LogId = log.Id
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Exception in SendImageWithCtaAsync: " + ex.Message);

                await _db.MessageLogs.AddAsync(new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TextContent ?? "[Image CTA Failed]",
                    RenderedBody = dto.TextContent ?? "[Failed image CTA]",
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    RawResponse = ex.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId,
                });

                await _db.SaveChangesAsync();

                return ResponseResult.ErrorInfo("❌ Failed to send image+CTA.", ex.ToString());
            }
        }
        [Obsolete("Use outbox + SendPayloadAsync via worker.")]
        public async Task<ResponseResult> SendImageTemplateMessageAsync(ImageTemplateMessageDto dto, Guid businessId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Provider) ||
                    (dto.Provider != "PINNACLE" && dto.Provider != "META_CLOUD"))
                {
                    return ResponseResult.ErrorInfo("❌ Invalid provider.",
                        "Provider must be exactly 'PINNACLE' or 'META_CLOUD'.");
                }

                var components = new List<object>();

                if (!string.IsNullOrWhiteSpace(dto.HeaderImageUrl))
                {
                    components.Add(new
                    {
                        type = "header",
                        parameters = new[]
                        {
                            new { type = "image", image = new { link = dto.HeaderImageUrl! } }
                        }
                    });
                }

                components.Add(new
                {
                    type = "body",
                    parameters = (dto.TemplateParameters ?? new List<string>())
                        .Select(p => new { type = "text", text = p })
                        .ToArray()
                });

                var btns = dto.ButtonParameters ?? new List<CampaignButtonDto>();
                for (int i = 0; i < btns.Count && i < 3; i++)
                {
                    var btn = btns[i];
                    var subType = btn.ButtonType?.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(subType)) continue;

                    var button = new Dictionary<string, object>
                    {
                        ["type"] = "button",
                        ["sub_type"] = subType,
                        ["index"] = i.ToString()
                    };

                    if (subType == "quick_reply" && !string.IsNullOrWhiteSpace(btn.TargetUrl))
                        button["parameters"] = new[] { new { type = "payload", payload = btn.TargetUrl! } };
                    else if (subType == "url" && !string.IsNullOrWhiteSpace(btn.TargetUrl))
                        button["parameters"] = new[] { new { type = "text", text = btn.TargetUrl! } };

                    components.Add(button);
                }

                var lang = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode!;

                var sendResult = await SendViaProviderAsync(
                    businessId,
                    dto.Provider,
                    p => p.SendTemplateAsync(dto.RecipientNumber, dto.TemplateName, lang, components),
                    dto.PhoneNumberId
                );

                var renderedBody = TemplateParameterHelper.FillPlaceholders(
                    dto.TemplateBody ?? "",
                    dto.TemplateParameters ?? new List<string>());

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TemplateName,
                    MediaUrl = dto.HeaderImageUrl,
                    RenderedBody = renderedBody,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId,
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success
                        ? "✅ Image template sent successfully."
                        : (sendResult.Message ?? "❌ WhatsApp API returned an error."),
                    Data = new { Success = sendResult.Success, MessageId = sendResult.MessageId, LogId = log.Id },
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    LogId = log.Id
                };
            }
            catch (Exception ex)
            {
                await _db.MessageLogs.AddAsync(new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TemplateName,
                    RenderedBody = TemplateParameterHelper.FillPlaceholders(dto.TemplateBody ?? "", dto.TemplateParameters ?? new List<string>()),
                    MediaUrl = dto.HeaderImageUrl,
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    RawResponse = ex.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId,
                });

                await _db.SaveChangesAsync();
                return ResponseResult.ErrorInfo("❌ Error sending image template.", ex.ToString());
            }
        }

        public async Task<IEnumerable<RecentMessageLogDto>> GetLogsByBusinessIdAsync(Guid businessId)
        {
            var logs = await _db.MessageLogs
                .Where(m => m.BusinessId == businessId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(1000)
                .Select(m => new RecentMessageLogDto
                {
                    Id = m.Id,
                    RecipientNumber = m.RecipientNumber,
                    MessageContent = m.MessageContent,
                    Status = m.Status,
                    CreatedAt = m.CreatedAt,
                    SentAt = m.SentAt,
                    ErrorMessage = m.ErrorMessage
                })
                .ToListAsync();

            return logs;
        }

        public Task<ResponseResult> SendDocumentTemplateMessageAsync(DocumentTemplateMessageDto dto, Guid businessId)
        {
            throw new NotImplementedException();
        }

        private async Task<IReadOnlyList<WhatsAppSettingEntity>> GetBusinessWhatsAppSettingsAsync(Guid businessId)
        {
            if (_settingsCache.TryGetValue(businessId, out var cached) && cached.expiresAt > DateTime.UtcNow)
                return cached.setting;

            var items = await _db.WhatsAppSettings
                .Where(s => s.BusinessId == businessId)
                .ToListAsync();

            if (items == null || items.Count == 0)
                throw new Exception("WhatsApp settings not found.");

            var ro = items.AsReadOnly();
            _settingsCache[businessId] = (ro, DateTime.UtcNow.AddMinutes(5));
            return ro;
        }
    }
}



