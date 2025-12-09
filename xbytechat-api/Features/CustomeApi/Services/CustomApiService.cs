using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json; 
using xbytechat.api.Features.CustomeApi.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;                       
using xbytechat_api.Features.Billing.Services;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat.api.Helpers;
using System.Text.RegularExpressions;      

namespace xbytechat.api.Features.CustomeApi.Services
{
    public sealed class CustomApiService : ICustomApiService
    {
        private readonly AppDbContext _context;
        private readonly IWhatsAppTemplateFetcherService _templateFetcher;
        private readonly IMessageEngineService _messageEngine;
        private readonly IBillingIngestService _billingIngest;
        private readonly ILogger<CustomApiService> _logger;

        public CustomApiService(
            AppDbContext context,
            IWhatsAppTemplateFetcherService templateFetcher,
            IMessageEngineService messageEngine,
            IBillingIngestService billingIngest,
            ILogger<CustomApiService> logger)
        {
            _context = context;
            _templateFetcher = templateFetcher;
            _messageEngine = messageEngine;
            _billingIngest = billingIngest;
            _logger = logger;
        }

        public async Task<ResponseResult> SendTemplateAsync(DirectTemplateSendRequest req, CancellationToken ct = default)
        {
            try
            {
                var toNormalized = NormalizePhone(req.To);
                var reqId = Guid.NewGuid();

                // 1) Resolve WhatsApp sender by phoneNumberId (across all businesses)
                //var ws = await _context.WhatsAppPhoneNumbers.AsNoTracking()
                //    .Where(s => s.IsActive && s.PhoneNumberId == req.PhoneNumberId)
                //    .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                //    .FirstOrDefaultAsync(ct);

                var ws = await _context.WhatsAppPhoneNumbers.AsNoTracking()
                    .Where(s =>  s.IsActive && s.PhoneNumberId == req.PhoneNumberId)
                    .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                   .FirstOrDefaultAsync(ct);



                if (ws == null)
                    return ResponseResult.ErrorInfo("❌ Active WhatsApp sender (phoneNumberId) not found.");

                var businessId = ws.BusinessId;
                var provider = (ws.Provider ?? "").Trim().ToUpperInvariant(); // "META_CLOUD" | "PINNACLE"
                if (provider != "META_CLOUD" && provider != "PINNACLE")
                    return ResponseResult.ErrorInfo($"❌ Unsupported provider: {provider}");

                _logger.LogInformation(
                    "[CustomAPI:{ReqId}] Begin send. biz={BusinessId} pnid={PhoneNumberId} to={MaskedTo} template={TemplateId}",
                    reqId, businessId, req.PhoneNumberId, Mask(toNormalized), req.TemplateId);

                // 2) Fetch template meta (for language + buttons)
                var meta = await _templateFetcher.GetTemplateByNameAsync(businessId, req.TemplateId, includeButtons: true);
                if (meta == null)
                    return ResponseResult.ErrorInfo("❌ Template metadata not found for the given templateId.");

                var languageCode = (meta.Language ?? "").Trim();
                if (string.IsNullOrWhiteSpace(languageCode))
                    return ResponseResult.ErrorInfo("❌ Template language not resolved from provider metadata.");

                // 3) Header decision
                var isVideoHeader = !string.IsNullOrWhiteSpace(req.VideoUrl);
                if (isVideoHeader && !IsHttpsMp4Url(req.VideoUrl, out var vErr))
                    return ResponseResult.ErrorInfo("🚫 Invalid VideoUrl.", vErr);

                // 4) Build components
                var (components, whyBuildFail) = BuildComponents(isVideoHeader, req.Variables, req.VideoUrl);
                if (components == null)
                {
                    _logger.LogWarning("[CustomAPI:{ReqId}] Component build failed: {Err}", reqId, whyBuildFail);
                    return ResponseResult.ErrorInfo($"🚫 Component build failed: {whyBuildFail}");
                }

                // 5) Snapshot first 3 buttons (optional analytics)
                string? buttonBundleJson = null;
                try
                {
                    if (meta.ButtonParams is { Count: > 0 })
                    {
                        var bundle = meta.ButtonParams.Take(3)
                            .Select((b, i) => new
                            {
                                i,
                                position = i + 1,
                                text = (b.Text ?? "").Trim(),
                                type = b.Type,
                                subType = b.SubType
                            }).ToList();
                        buttonBundleJson = JsonConvert.SerializeObject(bundle);
                    }
                }
                catch { /* best-effort snapshot */ }

                // 6) Entry step for linked flow (optional)
                Guid? entryStepId = null;
                if (req.FlowConfigId.HasValue)
                {
                    entryStepId = await _context.CTAFlowSteps
                        .Where(s => s.CTAFlowConfigId == req.FlowConfigId.Value)
                        .OrderBy(s => s.StepOrder)
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefaultAsync(ct);
                }

                // 7) Build provider payload
                var languageField = new { policy = "deterministic", code = string.IsNullOrWhiteSpace(languageCode) ? "en_US" : languageCode };
                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = toNormalized,
                    type = "template",
                    template = new
                    {
                        name = req.TemplateId,
                        language = languageField,
                        components
                    }
                };

                _logger.LogInformation("[CustomAPI:{ReqId}] Sending {Template} to {To} via {Provider} (PNID={PNID}) video={Video}",
                    reqId, req.TemplateId, Mask(toNormalized), provider, req.PhoneNumberId, isVideoHeader);

                // 8) Send
                var result = await _messageEngine.SendPayloadAsync(
                    businessId: businessId,
                    provider: provider,
                    payload: payload,
                    phoneNumberId: req.PhoneNumberId
                );

                // 9) Log + billing
                var now = DateTime.UtcNow;
                var logId = Guid.NewGuid();

                _context.MessageLogs.Add(new MessageLog
                {
                    Id = logId,
                    BusinessId = businessId,
                    CampaignId = null,
                    RecipientNumber = toNormalized,
                    MessageContent = req.TemplateId,
                    MediaUrl = isVideoHeader ? req.VideoUrl : null,
                    Status = result.Success ? "Sent" : "Failed",
                    MessageId = result.MessageId,          // or just ProviderMessageId; keep one if you want to de-dup
                    ProviderMessageId = result.MessageId,
                    ErrorMessage = result.ErrorMessage,
                    RawResponse = result.RawResponse,
                    CreatedAt = now,
                    SentAt = result.Success ? now : (DateTime?)null,
                    Source = "custom_api",
                    Provider = provider,
                    CTAFlowConfigId = req.FlowConfigId,
                    CTAFlowStepId = entryStepId,
                    ButtonBundleJson = buttonBundleJson
                });

                await _context.SaveChangesAsync(ct);

                await _billingIngest.IngestFromSendResponseAsync(
                    businessId: businessId,
                    messageLogId: logId,
                    provider: provider,
                    rawResponseJson: result.RawResponse ?? "{}"
                );

                _logger.LogInformation("[CustomAPI:{ReqId}] Done. success={Success} msgId={MessageId} flow={Flow} step={Step}",
                    reqId, result.Success, result.MessageId, req.FlowConfigId, entryStepId);

                return result.Success
                    ? ResponseResult.SuccessInfo("🚀 Template sent.",
                        new
                        {
                            messageId = result.MessageId,
                            to = toNormalized,
                            templateId = req.TemplateId,
                            flowConfigId = req.FlowConfigId,
                            flowEntryStepId = entryStepId
                        })
                    : ResponseResult.ErrorInfo("❌ Send failed.", result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception in CustomApiService.SendTemplateAsync");
                return ResponseResult.ErrorInfo("🚨 Server error while sending template.", ex.ToString());
            }
        }

        // ===== helpers (unchanged) =====
        private static string NormalizePhone(string raw) => raw.StartsWith("+") ? raw[1..] : raw;
        private static string Mask(string phone) => phone.Length <= 6 ? phone : $"{new string('*', phone.Length - 4)}{phone[^4..]}";
        private static bool IsHttpsMp4Url(string? url, out string? err)
        {
            err = null;
            if (string.IsNullOrWhiteSpace(url)) { err = "VideoUrl is required when sending a VIDEO header."; return false; }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) { err = "VideoUrl must be an absolute URL."; return false; }
            if (u.Scheme != Uri.UriSchemeHttps) { err = "VideoUrl must be HTTPS."; return false; }
            if (!u.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) { err = "VideoUrl must point to an .mp4 file."; return false; }
            return true;
        }
        //private static (List<object>? components, string? whyFail) BuildComponents(bool addVideoHeader, Dictionary<string, string>? variables, string? videoUrl)
        //{
        //    try
        //    {
        //        var components = new List<object>();
        //        if (addVideoHeader)
        //        {
        //            components.Add(new
        //            {
        //                type = "header",
        //                parameters = new object[] { new { type = "video", video = new { link = videoUrl } } }
        //            });
        //        }
        //        if (variables is { Count: > 0 })
        //        {
        //            var bodyParams = variables
        //                .Select(kv => (Index: int.TryParse(kv.Key, out var n) ? n : int.MaxValue, Text: kv.Value ?? string.Empty))
        //                .OrderBy(x => x.Index)
        //                .Select(x => new { type = "text", text = x.Text })
        //                .ToArray();

        //            if (bodyParams.Length > 0)
        //                components.Add(new { type = "body", parameters = bodyParams });
        //        }
        //        return (components, null);
        //    }
        //    catch (Exception ex) { return (null, ex.Message); }
        //}
        private static (List<object>? components, string? whyFail) BuildComponents(
         bool addVideoHeader,
         Dictionary<string, string>? variables,
         string? videoUrl)
        {
            try
            {
                var components = new List<object>();

                // Header (optional video)
                if (addVideoHeader)
                {
                    components.Add(new
                    {
                        type = "header",
                        parameters = new object[]
                        {
                    new { type = "video", video = new { link = videoUrl } }
                        }
                    });
                }

                // Body params ({{1}}, {{2}}, ...) — tolerate keys like "1", "2", "para1", "foo2"
                if (variables is { Count: > 0 })
                {
                    var list = variables.ToList(); // preserves insertion order for non-numbered keys

                    var bodyParams = list
                        .Select((kv, idx) =>
                        {
                            var m = Regex.Match(kv.Key ?? string.Empty, @"\d+");

                            int n = 0; // declare first so it's always definitely assigned
                            bool hasNum = m.Success && int.TryParse(m.Value, out n) && n > 0;

                            // Numbered keys come first ordered by n; others follow in insertion order
                            int orderKey = hasNum ? n : int.MaxValue - (list.Count - idx);

                            return new { Order = orderKey, Text = kv.Value ?? string.Empty };
                        })
                        .OrderBy(x => x.Order)
                        .Select(x => new { type = "text", text = x.Text })
                        .ToArray();

                    if (bodyParams.Length > 0)
                        components.Add(new { type = "body", parameters = bodyParams });
                }


                return (components, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }


    }
}

//using System;
//using System.Linq;
//using System.Collections.Generic;
//using System.Security.Claims;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using Microsoft.AspNetCore.Http;
//using Newtonsoft.Json;
//using xbytechat.api.Features.CustomeApi.DTOs;
//using xbytechat.api.Features.MessagesEngine.Services; // IMessageEngineService
//using xbytechat.api.Features.TemplateModule.Services; // IWhatsAppTemplateFetcherService
//using xbytechat.api.Models;                         // MessageLog
//using xbytechat.api.Shared;                         // ResponseResult
//using xbytechat_api.Features.Billing.Services;
//using xbytechat.api.Helpers;      // IBillingIngestService
//using xbytechat.api.WhatsAppSettings;
//using xbytechat_api.WhatsAppSettings.Services;
//namespace xbytechat.api.Features.CustomeApi.Services
//{
//    public sealed class CustomApiService : ICustomApiService
//    {
//        private readonly AppDbContext _context;
//        private readonly IHttpContextAccessor _http;
//        private readonly IWhatsAppTemplateFetcherService _templateFetcher;
//        private readonly IMessageEngineService _messageEngine;
//        private readonly IBillingIngestService _billingIngest;
//        private readonly ILogger<CustomApiService> _logger;

//        public CustomApiService(
//            AppDbContext context,
//            IHttpContextAccessor http,
//            IWhatsAppTemplateFetcherService templateFetcher,
//            IMessageEngineService messageEngine,
//            IBillingIngestService billingIngest,
//            ILogger<CustomApiService> logger)
//        {
//            _context = context;
//            _http = http;
//            _templateFetcher = templateFetcher;
//            _messageEngine = messageEngine;
//            _billingIngest = billingIngest;
//            _logger = logger;
//        }

//        public async Task<ResponseResult> SendTemplateAsync(DirectTemplateSendRequest req, CancellationToken ct = default)
//        {
//            try
//            {
//                // --- 0) Basic validation
//                if (string.IsNullOrWhiteSpace(req.PhoneNumberId))
//                    return ResponseResult.ErrorInfo("❌ phoneNumberId is required.");
//                if (string.IsNullOrWhiteSpace(req.To))
//                    return ResponseResult.ErrorInfo("❌ 'to' (recipient) is required.");
//                if (string.IsNullOrWhiteSpace(req.TemplateId))
//                    return ResponseResult.ErrorInfo("❌ templateId is required.");

//                var businessId = GetBusinessIdOrThrow();
//                var toNormalized = NormalizePhone(req.To);

//                var reqId = Guid.NewGuid();
//                _logger.LogInformation(
//                    "[CustomAPI:{ReqId}] Begin send. biz={BusinessId} pnid={PhoneNumberId} to={MaskedTo} template={TemplateId}",
//                    reqId, businessId, req.PhoneNumberId, Mask(toNormalized), req.TemplateId);

//                // --- 1) Resolve provider by phoneNumberId for this Business
//                var ws = await _context.WhatsAppSettings.AsNoTracking()
//                    .Where(s => s.BusinessId == businessId && s.IsActive && s.PhoneNumberId == req.PhoneNumberId)
//                    .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
//                    .FirstOrDefaultAsync(ct);

//                if (ws == null)
//                    return ResponseResult.ErrorInfo("❌ Active WhatsApp sender (phoneNumberId) not found for this Business.");

//                var provider = (ws.Provider ?? "").Trim().ToUpperInvariant(); // "META_CLOUD" | "PINNACLE"
//                if (provider != "META_CLOUD" && provider != "PINNACLE")
//                    return ResponseResult.ErrorInfo($"❌ Unsupported provider configured for this sender: {provider}");

//                // --- 2) Fetch template meta
//                // NOTE: your metadata doesn't expose HeaderType; we just read language & buttons. 
//                var meta = await _templateFetcher.GetTemplateByNameAsync(businessId, req.TemplateId, includeButtons: true);
//                if (meta == null)
//                    return ResponseResult.ErrorInfo("❌ Template metadata not found for the given templateId.");

//                var languageCode = (meta.Language ?? "").Trim();
//                if (string.IsNullOrWhiteSpace(languageCode))
//                    return ResponseResult.ErrorInfo("❌ Template language not resolved from provider metadata.");

//                // Decide header by request: if VideoUrl present -> add VIDEO header; otherwise TEXT-only
//                var isVideoHeader = !string.IsNullOrWhiteSpace(req.VideoUrl);
//                if (isVideoHeader && !IsHttpsMp4Url(req.VideoUrl, out var vErr))
//                    return ResponseResult.ErrorInfo("🚫 Invalid VideoUrl.", vErr);

//                // --- 3) Build components (TEXT or VIDEO)
//                var (components, whyBuildFail) = BuildComponents(isVideoHeader, req.Variables, req.VideoUrl);
//                if (components == null)
//                {
//                    _logger.LogWarning("[CustomAPI:{ReqId}] Component build failed: {Err}", reqId, whyBuildFail);
//                    return ResponseResult.ErrorInfo($"🚫 Component build failed: {whyBuildFail}");
//                }

//                // Snapshot first 3 buttons (if any) for analytics/click mapping (same as campaigns)
//                string? buttonBundleJson = null;
//                try
//                {
//                    if (meta.ButtonParams is { Count: > 0 })
//                    {
//                        var bundle = meta.ButtonParams.Take(3)
//                            .Select((b, i) => new
//                            {
//                                i,
//                                position = i + 1,
//                                text = (b.Text ?? "").Trim(),
//                                type = b.Type,
//                                subType = b.SubType
//                            }).ToList();
//                        buttonBundleJson = JsonConvert.SerializeObject(bundle);
//                    }
//                }
//                catch { /* best-effort snapshot */ }

//                // Find entry step of the linked flow (if provided)
//                Guid? entryStepId = null;
//                if (req.FlowConfigId.HasValue)
//                {
//                    entryStepId = await _context.CTAFlowSteps
//                        .Where(s => s.CTAFlowConfigId == req.FlowConfigId.Value)
//                        .OrderBy(s => s.StepOrder)
//                        .Select(s => (Guid?)s.Id)
//                        .FirstOrDefaultAsync(ct);
//                }


//                // Always object. Meta accepts { code: "en_US" } and ignores policy if present.
//                // Pinnacle REQUIRES an object.
//                var languageField = new
//                {
//                    policy = "deterministic",
//                    code = string.IsNullOrWhiteSpace(languageCode) ? "en_US" : languageCode
//                };

//                var payload = new
//                {
//                    messaging_product = "whatsapp",
//                    to = toNormalized,
//                    type = "template",
//                    template = new
//                    {
//                        name = req.TemplateId,
//                        language = languageField,
//                        components
//                    }
//                };


//                _logger.LogInformation("[CustomAPI:{ReqId}] Sending {Template} to {To} via {Provider} (PNID={PNID}) video={Video}",
//                    reqId, req.TemplateId, Mask(toNormalized), provider, req.PhoneNumberId, isVideoHeader);

//                var result = await _messageEngine.SendPayloadAsync(
//                    businessId: businessId,
//                    provider: provider,
//                    payload: payload,
//                    phoneNumberId: req.PhoneNumberId   // ✅ correct parameter
//                );

//                // --- 5) Persist MessageLog (and flow linkage), then billing
//                var now = DateTime.UtcNow;
//                var logId = Guid.NewGuid();

//                _context.MessageLogs.Add(new MessageLog
//                {
//                    Id = logId,
//                    BusinessId = businessId,
//                    CampaignId = null,                         // direct API path
//                    RecipientNumber = toNormalized,
//                    MessageContent = req.TemplateId,
//                    MediaUrl = isVideoHeader ? req.VideoUrl : null,
//                    Status = result.Success ? "Sent" : "Failed",
//                    MessageId = result.MessageId,
//                    ErrorMessage = result.ErrorMessage,
//                    RawResponse = result.RawResponse,
//                    CreatedAt = now,
//                    SentAt = result.Success ? now : (DateTime?)null,
//                    Source = "custom_api",
//                    Provider = provider,
//                    ProviderMessageId = result.MessageId,

//                    // 🔗 Store flow linkage like campaigns do
//                    CTAFlowConfigId = req.FlowConfigId,
//                    CTAFlowStepId = entryStepId,
//                    ButtonBundleJson = buttonBundleJson
//                });

//                await _context.SaveChangesAsync(ct);

//                await _billingIngest.IngestFromSendResponseAsync(
//                    businessId: businessId,
//                    messageLogId: logId,
//                    provider: provider,
//                    rawResponseJson: result.RawResponse ?? "{}"
//                );

//                _logger.LogInformation("[CustomAPI:{ReqId}] Done. success={Success} msgId={MessageId} flow={Flow} step={Step}",
//                    reqId, result.Success, result.MessageId, req.FlowConfigId, entryStepId);

//                return result.Success
//                    ? ResponseResult.SuccessInfo("🚀 Template sent.",
//                        new
//                        {
//                            messageId = result.MessageId,
//                            to = toNormalized,
//                            templateId = req.TemplateId,
//                            flowConfigId = req.FlowConfigId,
//                            flowEntryStepId = entryStepId
//                        })
//                    : ResponseResult.ErrorInfo("❌ Send failed.", result.ErrorMessage);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "❌ Exception in CustomApiService.SendTemplateAsync");
//                return ResponseResult.ErrorInfo("🚨 Server error while sending template.", ex.ToString());
//            }
//        }

//        // ===== helpers =====

//        private Guid GetBusinessIdOrThrow()
//        {
//            var user = _http.HttpContext?.User;
//            if (user == null) throw new InvalidOperationException("Missing HttpContext/User.");

//            var bid = user.FindFirstValue("BusinessId") ?? user.FindFirstValue("bid") ?? user.FindFirstValue("business_id");
//            if (string.IsNullOrWhiteSpace(bid)) throw new InvalidOperationException("BusinessId claim is missing.");
//            return Guid.Parse(bid);
//        }

//        private static string NormalizePhone(string raw) => raw.StartsWith("+") ? raw[1..] : raw;

//        private static string Mask(string phone)
//            => phone.Length <= 6 ? phone : $"{new string('*', phone.Length - 4)}{phone[^4..]}";

//        private static bool IsHttpsMp4Url(string? url, out string? err)
//        {
//            err = null;
//            if (string.IsNullOrWhiteSpace(url)) { err = "VideoUrl is required when sending a VIDEO header."; return false; }
//            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) { err = "VideoUrl must be an absolute URL."; return false; }
//            if (u.Scheme != Uri.UriSchemeHttps) { err = "VideoUrl must be HTTPS."; return false; }
//            if (!u.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) { err = "VideoUrl must point to an .mp4 file."; return false; }
//            return true;
//        }

//        private static (List<object>? components, string? whyFail) BuildComponents(
//            bool addVideoHeader,
//            Dictionary<string, string>? variables,
//            string? videoUrl)
//        {
//            try
//            {
//                var components = new List<object>();

//                // Header (optional video)
//                if (addVideoHeader)
//                {
//                    components.Add(new
//                    {
//                        type = "header",
//                        parameters = new object[]
//                        {
//                            new { type = "video", video = new { link = videoUrl } }
//                        }
//                    });
//                }

//                // Body params ({{1}}, {{2}}, ...)
//                if (variables is { Count: > 0 })
//                {
//                    var bodyParams = variables
//                        .Select(kv => (Index: int.TryParse(kv.Key, out var n) ? n : int.MaxValue, Text: kv.Value ?? string.Empty))
//                        .OrderBy(x => x.Index)
//                        .Select(x => new { type = "text", text = x.Text })
//                        .ToArray();

//                    if (bodyParams.Length > 0)
//                        components.Add(new { type = "body", parameters = bodyParams });
//                }

//                return (components, null);
//            }
//            catch (Exception ex)
//            {
//                return (null, ex.Message);
//            }
//        }
//    }
//}
