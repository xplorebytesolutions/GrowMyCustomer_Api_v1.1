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

            var payloadRecipient = TryExtractRecipientFromPayload(payload);
            if (string.IsNullOrWhiteSpace(payloadRecipient))
            {
                _logger.LogWarning(
                    "Outbound consent guard could not be applied because payload recipient is missing. businessId={BusinessId}",
                    businessId);
            }
            else
            {
                // Compliance guard must run before any outbound provider send call.
                var consentBlock = await EnforceOutboundConsentGuardAsync(businessId, payloadRecipient);
                if (consentBlock != null) return consentBlock;
            }

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

            var je = ToJsonElement(payload);

            // NEW: if the anonymous payload looks like a WhatsApp "template" message,
            // extract the parts and call SendTemplateAsync directly (no raw object pass-through).
            if (je.ValueKind == JsonValueKind.Object &&
                je.TryGetProperty("type", out var t) &&
                string.Equals(t.GetString(), "template", StringComparison.OrdinalIgnoreCase) &&
                je.TryGetProperty("to", out var toProp) &&
                toProp.ValueKind == JsonValueKind.String &&
                je.TryGetProperty("template", out var tmpl) &&
                tmpl.ValueKind == JsonValueKind.Object &&
                tmpl.TryGetProperty("name", out var nameProp) &&
                nameProp.ValueKind == JsonValueKind.String &&
                tmpl.TryGetProperty("language", out var langProp) &&
                langProp.ValueKind == JsonValueKind.Object &&
                langProp.TryGetProperty("code", out var codeProp) &&
                codeProp.ValueKind == JsonValueKind.String &&
                tmpl.TryGetProperty("components", out var comps) &&
                comps.ValueKind == JsonValueKind.Array)
            {
                var to = toProp.GetString();
                var name = nameProp.GetString();
                var code = codeProp.GetString();

                if (!string.IsNullOrWhiteSpace(to) &&
                    !string.IsNullOrWhiteSpace(name) &&
                    !string.IsNullOrWhiteSpace(code))
                {
                    try
                    {
                        var components = new List<object>();
                        foreach (var c in comps.EnumerateArray())
                        {
                            if (c.ValueKind != JsonValueKind.Object) continue;
                            if (!c.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String) continue;

                            var type = (typeProp.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                            if (type == "body")
                            {
                                var bodyParams = new List<object>();
                                if (c.TryGetProperty("parameters", out var pr) && pr.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var p in pr.EnumerateArray())
                                    {
                                        if (p.ValueKind != JsonValueKind.Object) continue;
                                        if (p.TryGetProperty("type", out var pt) &&
                                            pt.ValueKind == JsonValueKind.String &&
                                            string.Equals(pt.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                                            p.TryGetProperty("text", out var txt) &&
                                            txt.ValueKind == JsonValueKind.String)
                                        {
                                            bodyParams.Add(new { type = "text", text = txt.GetString() });
                                        }
                                    }
                                }
                                components.Add(new { type = "body", parameters = bodyParams.ToArray() });
                            }
                            else if (type == "header")
                            {
                                var headerParams = new List<object>();
                                if (c.TryGetProperty("parameters", out var pr) && pr.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var p in pr.EnumerateArray())
                                    {
                                        if (p.ValueKind != JsonValueKind.Object) continue;
                                        if (!p.TryGetProperty("type", out var pt) || pt.ValueKind != JsonValueKind.String) continue;

                                        var ptRaw = (pt.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                                        if (ptRaw == "image" &&
                                            p.TryGetProperty("image", out var imageObj) &&
                                            imageObj.ValueKind == JsonValueKind.Object &&
                                            imageObj.TryGetProperty("link", out var linkProp) &&
                                            linkProp.ValueKind == JsonValueKind.String)
                                        {
                                            headerParams.Add(new
                                            {
                                                type = "image",
                                                image = new { link = linkProp.GetString() }
                                            });
                                        }
                                    }
                                }
                                components.Add(new { type = "header", parameters = headerParams.ToArray() });
                            }
                            else if (type == "button")
                            {
                                if (!c.TryGetProperty("sub_type", out var subTypeProp) || subTypeProp.ValueKind != JsonValueKind.String) continue;
                                if (!c.TryGetProperty("index", out var indexProp) || indexProp.ValueKind != JsonValueKind.String) continue;

                                var subType = subTypeProp.GetString();
                                var index = indexProp.GetString();
                                if (!string.Equals(subType, "url", StringComparison.OrdinalIgnoreCase)) continue;

                                string? urlParam = null;
                                if (c.TryGetProperty("parameters", out var pr) && pr.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var p in pr.EnumerateArray())
                                    {
                                        if (p.ValueKind != JsonValueKind.Object) continue;
                                        if (p.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String)
                                        {
                                            urlParam = txt.GetString();
                                            break;
                                        }
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(urlParam))
                                {
                                    components.Add(new
                                    {
                                        type = "button",
                                        sub_type = "url",
                                        index,
                                        parameters = new object[] { new { type = "text", text = urlParam } }
                                    });
                                }
                            }
                        }

                        return await SendViaProviderAsync(businessId, provider,
                            p => p.SendTemplateAsync(to, name, code, components),
                            phoneNumberId);
                    }
                    catch
                    {
                        // Fall through to generic interactive path for malformed template components.
                    }
                }
            }

            // Fallback: avoid passing JsonElement directly to provider adapters.
            object interactivePayload = payload;
            if (payload is JsonElement payloadElement)
            {
                try
                {
                    interactivePayload = payloadElement.ValueKind switch
                    {
                        JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(payloadElement.GetRawText()) ?? payload,
                        JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(payloadElement.GetRawText()) ?? payload,
                        _ => payload
                    };
                }
                catch
                {
                    interactivePayload = payload;
                }
            }

            return await SendViaProviderAsync(businessId, provider, p => p.SendInteractiveAsync(interactivePayload), phoneNumberId);
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

        private static string NormalizeRecipientDigits(string? recipientNumber)
        {
            var raw = (recipientNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var normalized = PhoneNumberNormalizer.NormalizeToE164Digits(raw, "IN");
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;

            // Defensive fallback for legacy/loosely-formatted numbers.
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            return digits.Length is >= 7 and <= 15 ? digits : string.Empty;
        }

        private static List<string> BuildRecipientLookupCandidates(string? recipientNumber)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            var raw = (recipientNumber ?? string.Empty).Trim();
            var normalized = NormalizeRecipientDigits(raw);
            var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                candidates.Add(normalized);
                candidates.Add("+" + normalized);

                // Legacy IN contacts may still exist as local 10-digit numbers.
                if (normalized.Length == 12 && normalized.StartsWith("91", StringComparison.Ordinal))
                    candidates.Add(normalized.Substring(2));
            }

            if (!string.IsNullOrWhiteSpace(digitsOnly))
            {
                candidates.Add(digitsOnly);
                candidates.Add("+" + digitsOnly);

                if (digitsOnly.Length == 10)
                {
                    candidates.Add("91" + digitsOnly);
                    candidates.Add("+91" + digitsOnly);
                }
                else if (digitsOnly.Length == 12 && digitsOnly.StartsWith("91", StringComparison.Ordinal))
                {
                    candidates.Add(digitsOnly.Substring(2));
                }
            }

            if (!string.IsNullOrWhiteSpace(raw))
                candidates.Add(raw);

            return candidates.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }

        private static bool TryReadRecipientFromPayload(JsonElement root, out string? recipient)
        {
            recipient = null;
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (root.TryGetProperty("to", out var toProp) && toProp.ValueKind == JsonValueKind.String)
            {
                recipient = toProp.GetString();
                return !string.IsNullOrWhiteSpace(recipient);
            }

            if (root.TryGetProperty("To", out var toPascalProp) && toPascalProp.ValueKind == JsonValueKind.String)
            {
                recipient = toPascalProp.GetString();
                return !string.IsNullOrWhiteSpace(recipient);
            }

            if (root.TryGetProperty("recipient", out var recipientProp) && recipientProp.ValueKind == JsonValueKind.String)
            {
                recipient = recipientProp.GetString();
                return !string.IsNullOrWhiteSpace(recipient);
            }

            if (root.TryGetProperty("recipientNumber", out var recipientNumberProp) && recipientNumberProp.ValueKind == JsonValueKind.String)
            {
                recipient = recipientNumberProp.GetString();
                return !string.IsNullOrWhiteSpace(recipient);
            }

            if (root.TryGetProperty("Recipient", out var recipientPascalProp) && recipientPascalProp.ValueKind == JsonValueKind.String)
            {
                recipient = recipientPascalProp.GetString();
                return !string.IsNullOrWhiteSpace(recipient);
            }

            if (root.TryGetProperty("RecipientNumber", out var recipientNumberPascalProp) && recipientNumberPascalProp.ValueKind == JsonValueKind.String)
            {
                recipient = recipientNumberPascalProp.GetString();
                return !string.IsNullOrWhiteSpace(recipient);
            }

            return false;
        }

        private enum HeaderMediaReferenceKind
        {
            None = 0,
            HttpsLink = 1,
            MetaMediaId = 2
        }

        private sealed class HeaderMediaResolution
        {
            public HeaderMediaReferenceKind Kind { get; init; } = HeaderMediaReferenceKind.None;
            public string? Value { get; init; }
            public string? ErrorMessage { get; init; }
        }

        private static bool IsLikelyMetaMediaId(string? value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v)) return false;
            if (v.StartsWith("handle:", StringComparison.OrdinalIgnoreCase)) return false;
            if (v.StartsWith("id:", StringComparison.OrdinalIgnoreCase)) return false;
            if (v.Contains("://", StringComparison.Ordinal)) return false;
            if (v.Any(char.IsWhiteSpace)) return false;
            return true;
        }

        private static HeaderMediaResolution ResolveHeaderMediaReference(string? headerMediaUrl, bool isMetaCloud)
        {
            var raw = string.IsNullOrWhiteSpace(headerMediaUrl) ? null : headerMediaUrl.Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return new HeaderMediaResolution { Kind = HeaderMediaReferenceKind.None };

            if (raw.StartsWith("handle:", StringComparison.OrdinalIgnoreCase))
            {
                var handleValue = raw.Substring("handle:".Length).Trim();
                if (string.IsNullOrWhiteSpace(handleValue))
                {
                    return new HeaderMediaResolution
                    {
                        ErrorMessage = "Header media handle is empty."
                    };
                }

                if (!isMetaCloud)
                {
                    return new HeaderMediaResolution
                    {
                        ErrorMessage = "Uploaded media handle is supported only for META_CLOUD."
                    };
                }

                return new HeaderMediaResolution
                {
                    Kind = HeaderMediaReferenceKind.MetaMediaId,
                    Value = handleValue
                };
            }

            if (raw.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            {
                var idValue = raw.Substring("id:".Length).Trim();
                if (string.IsNullOrWhiteSpace(idValue))
                {
                    return new HeaderMediaResolution
                    {
                        ErrorMessage = "Header media id is empty."
                    };
                }

                if (!isMetaCloud)
                {
                    return new HeaderMediaResolution
                    {
                        ErrorMessage = "Header media id is supported only for META_CLOUD."
                    };
                }

                return new HeaderMediaResolution
                {
                    Kind = HeaderMediaReferenceKind.MetaMediaId,
                    Value = idValue
                };
            }

            if (Uri.TryCreate(raw, UriKind.Absolute, out var parsed))
            {
                if (string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return new HeaderMediaResolution
                    {
                        Kind = HeaderMediaReferenceKind.HttpsLink,
                        Value = raw
                    };
                }

                return new HeaderMediaResolution
                {
                    ErrorMessage = "Header media URL must be an absolute HTTPS URL."
                };
            }

            if (isMetaCloud && IsLikelyMetaMediaId(raw))
            {
                return new HeaderMediaResolution
                {
                    Kind = HeaderMediaReferenceKind.MetaMediaId,
                    Value = raw
                };
            }

            return new HeaderMediaResolution
            {
                ErrorMessage = isMetaCloud
                    ? "Provide a valid HTTPS URL or Meta media handle/id."
                    : "Provide a valid HTTPS URL."
            };
        }

        private static JsonElement ToJsonElement(object payload)
        {
            if (payload is JsonElement je) return je;
            if (payload is string s)
            {
                var trimmed = s.TrimStart();
                if (trimmed.StartsWith("{", StringComparison.Ordinal) ||
                    trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    try
                    {
                        using var stringDoc = JsonDocument.Parse(s);
                        return stringDoc.RootElement.Clone();
                    }
                    catch
                    {
                        // Fallback to serializer path below for non-JSON strings.
                    }
                }
            }
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            return doc.RootElement.Clone();
        }

        private async Task<(string Provider, string PhoneNumberId)> ResolveProviderAndSenderAsync(
            Guid businessId,
            string? requestedProvider,
            string? requestedPhoneNumberId,
            bool allowPinnacle = true)
        {
            var provider = string.IsNullOrWhiteSpace(requestedProvider)
                ? null
                : requestedProvider.Trim().ToUpperInvariant();
            requestedPhoneNumberId = string.IsNullOrWhiteSpace(requestedPhoneNumberId)
                ? null
                : requestedPhoneNumberId.Trim();

            if (string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(requestedPhoneNumberId))
            {
                provider = await _db.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(x => x.BusinessId == businessId &&
                                x.IsActive &&
                                x.PhoneNumberId == requestedPhoneNumberId)
                    .Select(x => x.Provider)
                    .FirstOrDefaultAsync();
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                var defPhone = await _db.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(x => x.BusinessId == businessId && x.IsActive)
                    .OrderByDescending(x => x.IsDefault)
                    .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                    .Select(x => new { x.Provider, x.PhoneNumberId })
                    .FirstOrDefaultAsync();

                if (defPhone != null)
                {
                    provider ??= (defPhone.Provider ?? string.Empty).Trim().ToUpperInvariant();
                    requestedPhoneNumberId ??= defPhone.PhoneNumberId;
                }
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                provider = await _db.WhatsAppSettings
                    .AsNoTracking()
                    .Where(s => s.BusinessId == businessId && s.IsActive)
                    .Select(s => s.Provider)
                    .FirstOrDefaultAsync();
            }

            provider = (provider ?? string.Empty).Trim().ToUpperInvariant();

            if (provider != "META_CLOUD" && provider != "PINNACLE")
                throw new InvalidOperationException("No valid WhatsApp provider configured.");

            if (!allowPinnacle && provider == "PINNACLE")
                throw new InvalidOperationException("PINNACLE not allowed for this path.");

            if (!string.IsNullOrWhiteSpace(requestedPhoneNumberId))
            {
                var senderProvider = await _db.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(x =>
                        x.BusinessId == businessId &&
                        x.IsActive &&
                        x.PhoneNumberId == requestedPhoneNumberId)
                    .Select(x => x.Provider)
                    .FirstOrDefaultAsync();

                var senderMatchesProvider = !string.IsNullOrWhiteSpace(senderProvider) &&
                                            string.Equals(senderProvider, provider, StringComparison.OrdinalIgnoreCase);

                if (!senderMatchesProvider)
                    throw new InvalidOperationException("Provided PhoneNumberId does not belong to the selected provider.");
            }

            if (string.IsNullOrWhiteSpace(requestedPhoneNumberId))
            {
                requestedPhoneNumberId = await _db.WhatsAppPhoneNumbers
                    .AsNoTracking()
                    .Where(x => x.BusinessId == businessId && x.IsActive && string.Equals(x.Provider, provider, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.IsDefault)
                    .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                    .Select(x => x.PhoneNumberId)
                    .FirstOrDefaultAsync();
            }

            if (provider == "META_CLOUD" && string.IsNullOrWhiteSpace(requestedPhoneNumberId))
                throw new InvalidOperationException("Missing PhoneNumberId for META_CLOUD.");

            return (provider, requestedPhoneNumberId ?? string.Empty);
        }

        private static string? TryExtractRecipientFromPayload(object payload)
        {
            try
            {
                var je = ToJsonElement(payload);
                return TryReadRecipientFromPayload(je, out var fromPayloadObject) ? fromPayloadObject : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<ResponseResult?> EnforceOutboundConsentGuardAsync(Guid businessId, string? recipientNumber, CancellationToken ct = default)
        {
            var lookupCandidates = BuildRecipientLookupCandidates(recipientNumber);
            var normalizedPhone = NormalizeRecipientDigits(recipientNumber);
            if (lookupCandidates.Count == 0)
                return null;

            var contact = await _db.Contacts
                .Where(c => c.BusinessId == businessId && lookupCandidates.Contains(c.PhoneNumber))
                .OrderByDescending(c => c.OptStatus == ContactOptStatus.OptedOut)
                .ThenByDescending(c => c.OptStatusUpdatedAt)
                .FirstOrDefaultAsync(ct);

            if (contact == null)
            {
                _logger.LogWarning(
                    "Outbound consent guard could not be applied because contact was not found. businessId={BusinessId} phone={Phone}",
                    businessId,
                    normalizedPhone);
                return null;
            }

            if (contact.OptStatus == ContactOptStatus.OptedOut)
            {
                _logger.LogInformation(
                    "Outbound blocked by consent guard. businessId={BusinessId} phone={Phone} optStatus={OptStatus} channelStatus={ChannelStatus}",
                    businessId,
                    normalizedPhone,
                    contact.OptStatus,
                    contact.ChannelStatus);
                return ResponseResult.ErrorInfo("CONTACT_OPTED_OUT");
            }

            if (contact.ChannelStatus != ContactChannelStatus.Valid)
            {
                _logger.LogInformation(
                    "Outbound blocked by channel guard. businessId={BusinessId} phone={Phone} optStatus={OptStatus} channelStatus={ChannelStatus}",
                    businessId,
                    normalizedPhone,
                    contact.OptStatus,
                    contact.ChannelStatus);
                return ResponseResult.ErrorInfo("CONTACT_CHANNEL_BLOCKED_OR_INVALID");
            }

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

        private static string NormalizeSnapshotHeaderType(string? headerKind, string? mediaUrl)
        {
            var kind = (headerKind ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(kind)) return kind;
            return string.IsNullOrWhiteSpace(mediaUrl) ? "none" : "image";
        }

        private static string? TryExtractFilenameFromMediaUrl(string? mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl)) return null;
            try
            {
                if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
                {
                    var fileName = System.IO.Path.GetFileName(uri.LocalPath);
                    return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
                }
            }
            catch { }
            return null;
        }

        private static List<object> BuildButtonsSnapshot(string? templateUrlButtonsJson, IReadOnlyList<string>? urlButtonParams)
        {
            var buttons = new List<object>();
            var dynamicValues = (urlButtonParams ?? Array.Empty<string>()).Take(3).ToArray();

            if (!string.IsNullOrWhiteSpace(templateUrlButtonsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(templateUrlButtonsJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var i = 0;
                        foreach (var btn in doc.RootElement.EnumerateArray())
                        {
                            var type = btn.TryGetProperty("type", out var t) ? t.GetString() : null;
                            type ??= btn.TryGetProperty("buttonType", out var bt) ? bt.GetString() : null;
                            type ??= btn.TryGetProperty("sub_type", out var st) ? st.GetString() : null;
                            type = string.IsNullOrWhiteSpace(type) ? "url" : type.Trim().ToLowerInvariant();

                            var text = btn.TryGetProperty("text", out var txt) ? txt.GetString() : null;
                            text ??= btn.TryGetProperty("title", out var title) ? title.GetString() : null;
                            text = string.IsNullOrWhiteSpace(text) ? $"Button {i + 1}" : text.Trim();

                            var value = (i < dynamicValues.Length ? dynamicValues[i] : null)?.Trim();
                            if (string.IsNullOrWhiteSpace(value))
                            {
                                value = btn.TryGetProperty("url", out var url) ? url.GetString() : null;
                                value ??= btn.TryGetProperty("value", out var val) ? val.GetString() : null;
                                value ??= btn.TryGetProperty("targetUrl", out var target) ? target.GetString() : null;
                            }

                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                buttons.Add(new { type, text, value = value.Trim() });
                            }
                            i++;
                        }
                    }
                }
                catch { }
            }

            if (buttons.Count == 0 && dynamicValues.Length > 0)
            {
                for (var i = 0; i < dynamicValues.Length; i++)
                {
                    var value = dynamicValues[i]?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        buttons.Add(new { type = "url", text = $"Button {i + 1}", value });
                }
            }

            return buttons;
        }

        private static string BuildTemplateSnapshotJson(
            string? headerKind,
            string? headerText,
            string? headerMediaUrl,
            string? bodyText,
            string? footerText,
            List<object>? buttons)
        {
            var normalizedHeaderType = NormalizeSnapshotHeaderType(headerKind, headerMediaUrl);
            var snapshot = new
            {
                header = new
                {
                    type = normalizedHeaderType,
                    text = string.IsNullOrWhiteSpace(headerText) ? null : headerText.Trim(),
                    mediaUrl = string.IsNullOrWhiteSpace(headerMediaUrl) ? null : headerMediaUrl.Trim(),
                    filename = normalizedHeaderType == "document" ? TryExtractFilenameFromMediaUrl(headerMediaUrl) : null
                },
                body = new
                {
                    text = string.IsNullOrWhiteSpace(bodyText) ? string.Empty : bodyText
                },
                footer = new
                {
                    text = string.IsNullOrWhiteSpace(footerText) ? null : footerText.Trim()
                },
                buttons = buttons ?? new List<object>()
            };

            return JsonSerializer.Serialize(snapshot);
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

                var consentBlock = await EnforceOutboundConsentGuardAsync(dto.BusinessId, dto.RecipientNumber);
                if (consentBlock != null) return consentBlock;

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
                var snapshotJson = BuildTemplateSnapshotJson(
                    headerKind: "none",
                    headerText: null,
                    headerMediaUrl: null,
                    bodyText: resolvedBody,
                    footerText: null,
                    buttons: new List<object>());

                // Log result
                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TemplateName ?? "N/A",
                    RenderedBody = resolvedBody,
                    MessageKind = MessageKind.Template,
                    TemplateName = dto.TemplateName,
                    TemplateLanguage = "en_US",
                    TemplateSnapshotJson = snapshotJson,
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
                    MessageKind = MessageKind.Template,
                    TemplateName = dto.TemplateName,
                    TemplateLanguage = "en_US",
                    TemplateSnapshotJson = BuildTemplateSnapshotJson(
                        headerKind: "none",
                        headerText: null,
                        headerMediaUrl: null,
                        bodyText: TemplateParameterHelper.FillPlaceholders(
                            dto.TemplateBody ?? "",
                            dto.TemplateParameters?.Values.ToList() ?? new List<string>()),
                        footerText: null,
                        buttons: new List<object>()),
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
                var snapshotJson = BuildTemplateSnapshotJson(
                    headerKind: "video",
                    headerText: null,
                    headerMediaUrl: dto.HeaderVideoUrl,
                    bodyText: renderedBody,
                    footerText: null,
                    buttons: new List<object>());

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = dto.RecipientNumber!,
                    MessageContent = dto.TemplateName!,
                    MediaUrl = dto.HeaderVideoUrl,
                    RenderedBody = renderedBody,
                    MessageKind = MessageKind.Template,
                    TemplateName = dto.TemplateName,
                    TemplateLanguage = langCode,
                    TemplateSnapshotJson = snapshotJson,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.ErrorMessage ?? (sendResult.Success ? null : "WhatsApp API returned an error."),
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    Provider = provider,
                    ProviderMessageId = sendResult.MessageId,
                    SentAt = sendResult.Success ? DateTime.UtcNow : null,
                    CreatedAt = DateTime.UtcNow,
                    Source = "direct",
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
                        MessageKind = MessageKind.Template,
                        TemplateName = dto.TemplateName,
                        TemplateLanguage = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode,
                        TemplateSnapshotJson = BuildTemplateSnapshotJson(
                            headerKind: "video",
                            headerText: null,
                            headerMediaUrl: dto.HeaderVideoUrl,
                            bodyText: TemplateParameterHelper.FillPlaceholders(dto.TemplateBody ?? "", dto.TemplateParameters ?? new List<string>()),
                            footerText: null,
                            buttons: new List<object>()),
                        Status = "Failed",
                        ErrorMessage = ex.Message,
                        CreatedAt = DateTime.UtcNow,
                        Source = "direct",
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
                var (providerUpper, phoneNumberId) = await ResolveProviderAndSenderAsync(
                    businessId,
                    dto.Provider,
                    dto.PhoneNumberId,
                    allowPinnacle: true);

                // Contact upsert/touch
                Guid? contactId = null;

                var recipientRaw = (dto.RecipientNumber ?? string.Empty).Trim();
                var recipientDigits = PhoneNumberNormalizer.NormalizeToE164Digits(recipientRaw, "IN");
                if (string.IsNullOrWhiteSpace(recipientDigits))
                    return ResponseResult.ErrorInfo("❌ Invalid recipient number.", $"Invalid/unsupported phone: '{recipientRaw}'");

                var consentBlock = await EnforceOutboundConsentGuardAsync(businessId, recipientDigits);
                if (consentBlock != null) return consentBlock;

                // Canonical lookup: digits-only E.164 (no '+')
                var contact = await _db.Contacts.FirstOrDefaultAsync(c =>
                    c.BusinessId == businessId &&
                    c.PhoneNumber == recipientDigits);

                if (contact != null)
                {
                    contactId = contact.Id;
                    contact.LastContactedAt = DateTime.UtcNow;

                    // If caller wants to save and provided a better name, backfill placeholders only.
                    if (dto.IsSaveContact && !string.IsNullOrWhiteSpace(dto.ContactName))
                    {
                        var preferredName = dto.ContactName.Trim();
                        if (!string.IsNullOrWhiteSpace(preferredName) &&
                            (string.IsNullOrWhiteSpace(contact.Name) ||
                             contact.Name == "WhatsApp User" ||
                             contact.Name == contact.PhoneNumber))
                        {
                            contact.Name = preferredName;
                        }
                    }
                }
                else if (dto.IsSaveContact)
                {
                    var preferredName = string.IsNullOrWhiteSpace(dto.ContactName)
                        ? null
                        : dto.ContactName.Trim();

                    contact = new Contact
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Name = preferredName ?? "WhatsApp User",
                        PhoneNumber = recipientDigits,
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
                    providerUpper,
                    p => p.SendTextAsync(recipientDigits, dto.TextContent),
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
                    RecipientNumber = recipientDigits,
                    MessageContent = dto.TextContent,
                    RenderedBody = dto.TextContent,
                    ContactId = contactId,
                    MediaUrl = null,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = sendResult.Success ? DateTime.UtcNow : null,
                    MessageId = messageId,
                    ProviderMessageId = messageId,
                    MessageKind = MessageKind.FreeformText,
                    Source = dto.Source
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
                            CreatedAt = DateTime.UtcNow,
                            MessageKind = MessageKind.FreeformText
                        });
                        await _db.SaveChangesAsync();
                    }
                }
                catch { /* ignore */ }

                return ResponseResult.ErrorInfo("❌ Failed to send text message.", ex.ToString());
            }
        }

        public Task<ResponseResult> SendImageDirectAsync(MediaMessageSendDto dto)
            => SendMediaDirectAsync(dto, mediaType: "image");

        public Task<ResponseResult> SendDocumentDirectAsync(MediaMessageSendDto dto)
            => SendMediaDirectAsync(dto, mediaType: "document");

        public Task<ResponseResult> SendVideoDirectAsync(MediaMessageSendDto dto)
            => SendMediaDirectAsync(dto, mediaType: "video");

        public Task<ResponseResult> SendAudioDirectAsync(MediaMessageSendDto dto)
            => SendMediaDirectAsync(dto, mediaType: "audio");

        private async Task<ResponseResult> SendMediaDirectAsync(MediaMessageSendDto dto, string mediaType)
        {
            try
            {
                var businessId = _httpContextAccessor.HttpContext?.User?.GetBusinessId()
                    ?? throw new UnauthorizedAccessException("❌ Cannot resolve BusinessId from context.");

                if (dto == null) throw new ArgumentNullException(nameof(dto));

                var type = (mediaType ?? string.Empty).Trim().ToLowerInvariant();
                if (type is not ("image" or "document" or "video" or "audio"))
                    return ResponseResult.ErrorInfo("❌ Invalid media type.", "Media type must be 'image', 'document', 'video', or 'audio'.");

                var recipientRaw = (dto.RecipientNumber ?? string.Empty).Trim();
                var recipientDigits = PhoneNumberNormalizer.NormalizeToE164Digits(recipientRaw, "IN");
                if (string.IsNullOrWhiteSpace(recipientDigits))
                    return ResponseResult.ErrorInfo("❌ Invalid recipient number.", $"Invalid/unsupported phone: '{recipientRaw}'");

                var consentBlock = await EnforceOutboundConsentGuardAsync(businessId, recipientDigits);
                if (consentBlock != null) return consentBlock;

                var mediaId = (dto.MediaId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(mediaId))
                    return ResponseResult.ErrorInfo("❌ Missing media id.", "mediaId is required.");

                var (providerUpper, phoneNumberId) = await ResolveProviderAndSenderAsync(
                    businessId,
                    dto.Provider,
                    dto.PhoneNumberId,
                    allowPinnacle: false);

                Guid? contactId = dto.ContactId != Guid.Empty ? dto.ContactId : null;
                if (!contactId.HasValue)
                {
                    var contact = await _db.Contacts.FirstOrDefaultAsync(c =>
                        c.BusinessId == businessId &&
                        c.PhoneNumber == recipientDigits);
                    contactId = contact?.Id;
                }

                var caption = string.IsNullOrWhiteSpace(dto.Caption) ? null : dto.Caption.Trim();
                if (type == "audio" && !string.IsNullOrWhiteSpace(caption))
                    return ResponseResult.ErrorInfo("❌ Audio does not support captions.", "Remove Caption/Text when sending an audio message.");

                object payload = type switch
                {
                    "image" => new
                    {
                        messaging_product = "whatsapp",
                        to = recipientDigits,
                        type = "image",
                        image = new { id = mediaId, caption }
                    },
                    "document" => new
                    {
                        messaging_product = "whatsapp",
                        to = recipientDigits,
                        type = "document",
                        document = new { id = mediaId, caption, filename = dto.FileName }
                    },
                    "video" => new
                    {
                        messaging_product = "whatsapp",
                        to = recipientDigits,
                        type = "video",
                        video = new { id = mediaId, caption }
                    },
                    "audio" => new
                    {
                        messaging_product = "whatsapp",
                        to = recipientDigits,
                        type = "audio",
                        audio = new { id = mediaId }
                    },
                    _ => throw new InvalidOperationException("Unsupported media type.")
                };

                var sendResult = await SendViaProviderAsync(
                    businessId,
                    providerUpper,
                    p => p.SendInteractiveAsync(payload),
                    phoneNumberId
                );

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

                var rendered =
                    string.IsNullOrWhiteSpace(caption)
                        ? type switch
                        {
                            "image" => string.IsNullOrWhiteSpace(dto.FileName) ? "Image sent" : $"Image sent: {dto.FileName}",
                            "document" => string.IsNullOrWhiteSpace(dto.FileName) ? "PDF sent" : $"PDF sent: {dto.FileName}",
                            "video" => string.IsNullOrWhiteSpace(dto.FileName) ? "Video sent" : $"Video sent: {dto.FileName}",
                            "audio" => string.IsNullOrWhiteSpace(dto.FileName) ? "Audio sent" : $"Audio sent: {dto.FileName}",
                            _ => "Media sent"
                        }
                        : caption!;

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = recipientDigits,
                    MessageContent = rendered,
                    RenderedBody = rendered,
                    ContactId = contactId,
                    MediaUrl = null,
                    MediaId = mediaId,
                    MediaType = type,
                    FileName = dto.FileName,
                    MimeType = dto.MimeType,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = sendResult.Success ? DateTime.UtcNow : null,
                    MessageId = messageId,
                    ProviderMessageId = messageId,
                    Source = dto.Source,
                    MessageKind = MessageKind.Media
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success
                        ? "✅ Media message sent successfully."
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
                return ResponseResult.ErrorInfo("❌ Failed to send media message.", ex.ToString());
            }
        }

        public async Task<ResponseResult> SendLocationDirectAsync(LocationMessageSendDto dto)
        {
            try
            {
                var businessId = _httpContextAccessor.HttpContext?.User?.GetBusinessId()
                    ?? throw new UnauthorizedAccessException("❌ Cannot resolve BusinessId from context.");

                if (dto == null) throw new ArgumentNullException(nameof(dto));

                if (dto.Latitude < -90 || dto.Latitude > 90)
                    return ResponseResult.ErrorInfo("❌ Invalid latitude.", "Latitude must be between -90 and 90.");
                if (dto.Longitude < -180 || dto.Longitude > 180)
                    return ResponseResult.ErrorInfo("❌ Invalid longitude.", "Longitude must be between -180 and 180.");

                var recipientRaw = (dto.RecipientNumber ?? string.Empty).Trim();
                var recipientDigits = PhoneNumberNormalizer.NormalizeToE164Digits(recipientRaw, "IN");
                if (string.IsNullOrWhiteSpace(recipientDigits))
                    return ResponseResult.ErrorInfo("❌ Invalid recipient number.", $"Invalid/unsupported phone: '{recipientRaw}'");

                var consentBlock = await EnforceOutboundConsentGuardAsync(businessId, recipientDigits);
                if (consentBlock != null) return consentBlock;

                var (providerUpper, phoneNumberId) = await ResolveProviderAndSenderAsync(
                    businessId,
                    dto.Provider,
                    dto.PhoneNumberId,
                    allowPinnacle: false);

                object payload = new
                {
                    messaging_product = "whatsapp",
                    to = recipientDigits,
                    type = "location",
                    location = new
                    {
                        latitude = dto.Latitude,
                        longitude = dto.Longitude,
                        name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim(),
                        address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim()
                    }
                };

                var sendResult = await SendViaProviderAsync(
                    businessId,
                    providerUpper,
                    p => p.SendInteractiveAsync(payload),
                    phoneNumberId
                );

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

                var rendered = string.IsNullOrWhiteSpace(dto.Name)
                    ? "Location sent"
                    : dto.Name!.Trim();

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = recipientDigits,
                    MessageContent = rendered,
                    RenderedBody = rendered,
                    ContactId = dto.ContactId != Guid.Empty ? dto.ContactId : (Guid?)null,
                    MediaUrl = null,
                    MediaId = null,
                    MediaType = "location",
                    FileName = null,
                    MimeType = null,
                    LocationLatitude = dto.Latitude,
                    LocationLongitude = dto.Longitude,
                    LocationName = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim(),
                    LocationAddress = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = sendResult.Success ? DateTime.UtcNow : null,
                    MessageId = messageId,
                    ProviderMessageId = messageId,
                    Source = dto.Source,
                    MessageKind = MessageKind.Location
                };

                await _db.MessageLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                return new ResponseResult
                {
                    Success = sendResult.Success,
                    Message = sendResult.Success
                        ? "✅ Location sent successfully."
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
                return ResponseResult.ErrorInfo("❌ Failed to send location message.", ex.ToString());
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

                var consentBlock = await EnforceOutboundConsentGuardAsync(businessId, dto.RecipientNumber);
                if (consentBlock != null) return consentBlock;

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
                    SentAt = sendResult.Success ? DateTime.UtcNow : null,
                    MessageId = messageId,
                    Provider = dto.Provider,
                    ProviderMessageId = messageId,
                    MessageKind = MessageKind.FreeformText,
                    Source = dto.Source
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
                        CreatedAt = DateTime.UtcNow,
                        Provider = dto.Provider,
                        MessageKind = MessageKind.FreeformText,
                        Source = dto.Source
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
                var (providerUpper, phoneNumberId) = await ResolveProviderAndSenderAsync(
                    businessId,
                    dto.Provider,
                    dto.PhoneNumberId,
                    allowPinnacle: true);

                var consentBlock = await EnforceOutboundConsentGuardAsync(businessId, dto.RecipientNumber);
                if (consentBlock != null) return consentBlock;

                var templateMeta = await _db.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(t => t.BusinessId == businessId
                                && t.Provider.ToUpper() == providerUpper
                                && t.Name == dto.TemplateName)
                    .OrderByDescending(t => t.UpdatedAt > t.CreatedAt ? t.UpdatedAt : t.CreatedAt)
                    .Select(t => new
                    {
                        t.TemplateId,
                        t.Name,
                        t.LanguageCode,
                        t.HeaderKind,
                        t.HeaderText,
                        t.Body,
                        t.UrlButtons
                    })
                    .FirstOrDefaultAsync();

                // Build components (header + body + dynamic URL buttons)
                var components = new List<object>();

                var headerKind = (dto.HeaderKind ?? string.Empty).Trim().ToLowerInvariant();
                var headerUrl = string.IsNullOrWhiteSpace(dto.HeaderMediaUrl) ? null : dto.HeaderMediaUrl!.Trim();
                var isMetaCloud = string.Equals(providerUpper, "META_CLOUD", StringComparison.OrdinalIgnoreCase);
                var mediaResolution = ResolveHeaderMediaReference(headerUrl, isMetaCloud);
                if (!string.IsNullOrWhiteSpace(mediaResolution.ErrorMessage))
                {
                    return ResponseResult.ErrorInfo("❌ Invalid header media reference.", mediaResolution.ErrorMessage);
                }

                if (!string.IsNullOrWhiteSpace(headerUrl))
                {
                    object? headerParam = null;

                    if (headerKind == "image")
                    {
                        if (mediaResolution.Kind == HeaderMediaReferenceKind.MetaMediaId)
                            headerParam = new { type = "image", image = new { id = mediaResolution.Value } };
                        else if (mediaResolution.Kind == HeaderMediaReferenceKind.HttpsLink)
                            headerParam = new { type = "image", image = new { link = headerUrl } };
                        else
                            return ResponseResult.ErrorInfo("❌ Invalid image header URL.", "Use HTTPS URL or uploaded Meta media handle/id.");
                    }
                    else if (headerKind == "video")
                    {
                        if (mediaResolution.Kind == HeaderMediaReferenceKind.MetaMediaId)
                            headerParam = new { type = "video", video = new { id = mediaResolution.Value } };
                        else if (mediaResolution.Kind == HeaderMediaReferenceKind.HttpsLink)
                            headerParam = new { type = "video", video = new { link = headerUrl } };
                        else
                            return ResponseResult.ErrorInfo("❌ Invalid video header URL.", "Use HTTPS URL or uploaded Meta media handle/id.");
                    }
                    else if (headerKind == "document")
                    {
                        if (mediaResolution.Kind == HeaderMediaReferenceKind.MetaMediaId)
                            headerParam = new { type = "document", document = new { id = mediaResolution.Value } };
                        else if (mediaResolution.Kind == HeaderMediaReferenceKind.HttpsLink)
                            headerParam = new { type = "document", document = new { link = headerUrl } };
                        else
                            return ResponseResult.ErrorInfo("❌ Invalid document header URL.", "Use HTTPS URL or uploaded Meta media handle/id.");
                    }

                    if (headerParam != null)
                    {
                        components.Add(new
                        {
                            type = "header",
                            parameters = new object[] { headerParam }
                        });
                    }
                }

                var parameters = (dto.TemplateParameters ?? new List<string>())
                    .Select(p => new { type = "text", text = p })
                    .ToArray();

                if (parameters.Length > 0)
                    components.Add(new { type = "body", parameters });

                var urlParams = dto.UrlButtonParams ?? new List<string>();
                for (int i = 0; i < urlParams.Count && i < 3; i++)
                {
                    var p = (urlParams[i] ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(p)) continue;

                    components.Add(new
                    {
                        type = "button",
                        sub_type = "url",
                        index = i.ToString(),
                        parameters = new object[]
                        {
                            new { type = "text", text = p }
                        }
                    });
                }

                var lang = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode!;
                var resolvedBody = TemplateParameterHelper.FillPlaceholders(
                    dto.TemplateBody ?? templateMeta?.Body ?? string.Empty,
                    dto.TemplateParameters ?? new List<string>());
                if (string.IsNullOrWhiteSpace(resolvedBody))
                    resolvedBody = dto.TemplateBody ?? templateMeta?.Body ?? dto.TemplateName ?? string.Empty;

                var snapshotButtons = BuildButtonsSnapshot(templateMeta?.UrlButtons, urlParams);
                var snapshotJson = BuildTemplateSnapshotJson(
                    headerKind: headerKind,
                    headerText: templateMeta?.HeaderText,
                    headerMediaUrl: headerUrl,
                    bodyText: resolvedBody,
                    footerText: null,
                    buttons: snapshotButtons);

                _logger?.LogInformation(
                    "➡️ SEND-INTENT tmpl={Template} to={To} provider={Provider} pnid={PhoneNumberId} mode={Mode} headerKind={HeaderKind} headerRefKind={HeaderRefKind} ctaFlowConfig={CtaFlowConfigId} ctaFlowStep={CtaFlowStepId}",
                    dto.TemplateName,
                    dto.RecipientNumber,
                    providerUpper,
                    phoneNumberId ?? "(default)",
                    mode,
                    headerKind,
                    mediaResolution.Kind,
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
                    MediaUrl = headerUrl,
                    RenderedBody = resolvedBody,
                    MessageKind = MessageKind.Template,
                    TemplateName = dto.TemplateName,
                    TemplateLanguage = lang,
                    TemplateSnapshotJson = snapshotJson,

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
                        MediaUrl = string.IsNullOrWhiteSpace(dto.HeaderMediaUrl) ? null : dto.HeaderMediaUrl.Trim(),
                        RenderedBody = TemplateParameterHelper.FillPlaceholders(
                            dto.TemplateBody ?? string.Empty,
                            dto.TemplateParameters ?? new List<string>()),
                        MessageKind = MessageKind.Template,
                        TemplateName = dto.TemplateName,
                        TemplateLanguage = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode,
                        TemplateSnapshotJson = BuildTemplateSnapshotJson(
                            headerKind: dto.HeaderKind,
                            headerText: null,
                            headerMediaUrl: dto.HeaderMediaUrl,
                            bodyText: TemplateParameterHelper.FillPlaceholders(
                                dto.TemplateBody ?? string.Empty,
                                dto.TemplateParameters ?? new List<string>()),
                            footerText: null,
                            buttons: BuildButtonsSnapshot(null, dto.UrlButtonParams)),
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
                        SentAt = result.Success ? DateTime.UtcNow : (DateTime?)null,
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

                var templateMeta = await _db.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(w => w.BusinessId == businessId && w.Name == templateName)
                    .OrderByDescending(w => (w.UpdatedAt > w.CreatedAt ? w.UpdatedAt : w.CreatedAt))
                    .Select(w => new
                    {
                        w.TemplateId,
                        w.LanguageCode,
                        w.HeaderKind,
                        w.HeaderText,
                        w.Body,
                        w.UrlButtons
                    })
                    .FirstOrDefaultAsync();
                var lang = string.IsNullOrWhiteSpace(templateMeta?.LanguageCode) ? "en_US" : templateMeta!.LanguageCode;
                var resolvedTemplateId = !string.IsNullOrWhiteSpace(templateMeta?.TemplateId)
                    ? templateMeta!.TemplateId!
                    : (!string.IsNullOrWhiteSpace(campaign.TemplateId) ? campaign.TemplateId! : templateName);

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
                var messageLogs = new List<MessageLog>(recipients.Count);
                var sendLogs = new List<CampaignSendLog>(recipients.Count);

                foreach (var r in recipients)
                {
                    var contactId = r.ContactId ?? r.AudienceContactId;
                    var messageLogId = Guid.NewGuid();

                    if (string.IsNullOrWhiteSpace(r.Phone))
                    {
                        var missingPhoneBody = templateMeta?.Body ?? campaign.MessageTemplate ?? templateName;
                        var missingPhoneSnapshot = BuildTemplateSnapshotJson(
                            headerKind: templateMeta?.HeaderKind ?? (string.IsNullOrWhiteSpace(campaign.ImageUrl) ? "none" : "image"),
                            headerText: templateMeta?.HeaderText,
                            headerMediaUrl: campaign.ImageUrl,
                            bodyText: missingPhoneBody,
                            footerText: null,
                            buttons: BuildButtonsSnapshot(templateMeta?.UrlButtons, new List<string>()));

                        messageLogs.Add(new MessageLog
                        {
                            Id = messageLogId,
                            BusinessId = businessId,
                            ContactId = contactId,
                            RecipientNumber = string.Empty,
                            MessageContent = templateName,
                            RenderedBody = missingPhoneBody,
                            MediaUrl = campaign.ImageUrl,
                            MessageKind = MessageKind.Template,
                            TemplateName = templateName,
                            TemplateLanguage = lang,
                            TemplateSnapshotJson = missingPhoneSnapshot,
                            Status = "Failed",
                            ErrorMessage = "Recipient phone number is missing.",
                            RawResponse = null,
                            MessageId = null,
                            Provider = provider,
                            ProviderMessageId = null,
                            CreatedAt = DateTime.UtcNow,
                            SentAt = null,
                            Source = "campaign",
                            CampaignId = campaign.Id
                        });

                        sendLogs.Add(new CampaignSendLog
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaign.Id,
                            ContactId = contactId,
                            RecipientId = r.Id,
                            MessageLogId = messageLogId,
                            MessageBody = missingPhoneBody,
                            TemplateId = resolvedTemplateId,
                            MessageId = null,
                            SendStatus = "Failed",
                            ErrorMessage = "Recipient phone number is missing.",
                            SentAt = null,
                            CreatedBy = sentBy,
                            BusinessId = businessId,
                        });

                        failedIds.Add(r.Id);
                        fail++;
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

                    var resolvedBody = TemplateParameterHelper.FillPlaceholders(
                        templateMeta?.Body ?? campaign.MessageTemplate ?? string.Empty,
                        bodyParams.ToList());
                    if (string.IsNullOrWhiteSpace(resolvedBody))
                        resolvedBody = campaign.MessageTemplate ?? templateName;

                    var snapshotButtons = BuildButtonsSnapshot(
                        templateMeta?.UrlButtons,
                        Enumerable.Range(1, 3)
                            .Select(pos => buttonVars.TryGetValue($"button{pos}.url_param", out var v) ? v : string.Empty)
                            .ToList());
                    var snapshotJson = BuildTemplateSnapshotJson(
                        headerKind: templateMeta?.HeaderKind ?? (string.IsNullOrWhiteSpace(headerImage) ? "none" : "image"),
                        headerText: templateMeta?.HeaderText,
                        headerMediaUrl: headerImage,
                        bodyText: resolvedBody,
                        footerText: null,
                        buttons: snapshotButtons);

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
                    messageLogs.Add(new MessageLog
                    {
                        Id = messageLogId,
                        BusinessId = businessId,
                        ContactId = contactId,
                        RecipientNumber = r.Phone!,
                        MessageContent = templateName,
                        RenderedBody = resolvedBody,
                        MediaUrl = headerImage,
                        MessageKind = MessageKind.Template,
                        TemplateName = templateName,
                        TemplateLanguage = lang,
                        TemplateSnapshotJson = snapshotJson,
                        Status = result.Success ? "Sent" : "Failed",
                        ErrorMessage = result.Success ? null : result.Message,
                        RawResponse = result.RawResponse,
                        MessageId = result.MessageId,
                        Provider = provider,
                        ProviderMessageId = result.MessageId,
                        CreatedAt = DateTime.UtcNow,
                        SentAt = result.Success ? DateTime.UtcNow : null,
                        Source = "campaign",
                        CampaignId = campaign.Id
                    });

                    sendLogs.Add(new CampaignSendLog
                    {
                        Id = Guid.NewGuid(),
                        CampaignId = campaign.Id,
                        ContactId = contactId,
                        RecipientId = r.Id,
                        MessageLogId = messageLogId,
                        MessageBody = resolvedBody,
                        TemplateId = resolvedTemplateId,
                        MessageId = result.MessageId,
                        SendStatus = result.Success ? "Sent" : "Failed",
                        ErrorMessage = result.Success ? null : result.Message,
                        SentAt = result.Success ? DateTime.UtcNow : (DateTime?)null,
                        CreatedBy = sentBy,
                        BusinessId = businessId,
                    });

                    if (result.Success) { success++; successIds.Add(r.Id); } else { fail++; failedIds.Add(r.Id); }
                }

                if (messageLogs.Count > 0)
                    await _db.MessageLogs.AddRangeAsync(messageLogs);

                if (sendLogs.Count > 0)
                    await _db.CampaignSendLogs.AddRangeAsync(sendLogs);

                await _db.SaveChangesAsync();

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

                var consentBlock = await EnforceOutboundConsentGuardAsync(dto.BusinessId, dto.RecipientNumber);
                if (consentBlock != null) return consentBlock;

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

                object payload;
                if (string.IsNullOrWhiteSpace(dto.MediaUrl))
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = dto.RecipientNumber,
                        type = "interactive",
                        interactive = new
                        {
                            type = "button",
                            body = new { text = dto.TextContent },
                            action = new { buttons = validButtons }
                        }
                    };
                }
                else
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = dto.RecipientNumber,
                        type = "interactive",
                        interactive = new
                        {
                            type = "button",
                            header = new { type = "image", image = new { link = dto.MediaUrl } },
                            body = new { text = dto.TextContent },
                            action = new { buttons = validButtons }
                        }
                    };
                }

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
                    SentAt = sendResult.Success ? DateTime.UtcNow : null,
                    CTAFlowConfigId = dto.CTAFlowConfigId,
                    CTAFlowStepId = dto.CTAFlowStepId,
                    Provider = dto.Provider,
                    ProviderMessageId = messageId,
                    MessageKind = MessageKind.Media
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
                    Provider = dto.Provider,
                    MessageKind = MessageKind.Media
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

                var consentBlock = await EnforceOutboundConsentGuardAsync(businessId, dto.RecipientNumber);
                if (consentBlock != null) return consentBlock;

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
                var snapshotJson = BuildTemplateSnapshotJson(
                    headerKind: "image",
                    headerText: null,
                    headerMediaUrl: dto.HeaderImageUrl,
                    bodyText: renderedBody,
                    footerText: null,
                    buttons: new List<object>());

                var log = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto.TemplateName,
                    MediaUrl = dto.HeaderImageUrl,
                    RenderedBody = renderedBody,
                    MessageKind = MessageKind.Template,
                    TemplateName = dto.TemplateName,
                    TemplateLanguage = lang,
                    TemplateSnapshotJson = snapshotJson,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    ErrorMessage = sendResult.Success ? null : sendResult.Message,
                    RawResponse = sendResult.RawResponse,
                    MessageId = sendResult.MessageId,
                    Provider = dto.Provider,
                    ProviderMessageId = sendResult.MessageId,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = sendResult.Success ? DateTime.UtcNow : null,
                    Source = "direct",
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
                    MessageKind = MessageKind.Template,
                    TemplateName = dto.TemplateName,
                    TemplateLanguage = string.IsNullOrWhiteSpace(dto.LanguageCode) ? "en_US" : dto.LanguageCode,
                    TemplateSnapshotJson = BuildTemplateSnapshotJson(
                        headerKind: "image",
                        headerText: null,
                        headerMediaUrl: dto.HeaderImageUrl,
                        bodyText: TemplateParameterHelper.FillPlaceholders(dto.TemplateBody ?? "", dto.TemplateParameters ?? new List<string>()),
                        footerText: null,
                        buttons: new List<object>()),
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    RawResponse = ex.ToString(),
                    Provider = dto.Provider,
                    CreatedAt = DateTime.UtcNow,
                    Source = "direct",
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



