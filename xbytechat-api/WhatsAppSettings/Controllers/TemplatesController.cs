using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing;
using xbytechat.api.WhatsAppSettings.Services;
using xbytechat_api.WhatsAppSettings.Models;
using xbytechat_api.WhatsAppSettings.Services;
namespace xbytechat.api.WhatsAppSettings.Controllers
{
    [ApiController]
    [Route("api/templates")]
    public class TemplatesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITemplateSyncService _sync;
        private readonly IWhatsAppTemplateFetcherService _fetcher;

        public TemplatesController(AppDbContext db, ITemplateSyncService sync, IWhatsAppTemplateFetcherService fetcher)
        { _db = db; _sync = sync; _fetcher = fetcher; }


        // Sync Templates
        ////[HttpPost("sync/{businessId:guid}")]
        ////[Authorize]
        ////public async Task<IActionResult> Sync(Guid businessId, [FromQuery] bool force = false)
        ////{
        ////    if (businessId == Guid.Empty) return BadRequest(new { success = false, message = "Invalid businessId" });
        ////    var result = await _sync.SyncBusinessTemplatesAsync(businessId, force);
        ////    return Ok(new { success = true, result });
        ////}
        [HttpPost("sync/{businessId:guid}")]
        [Authorize]
        public async Task<IActionResult> Sync(Guid businessId)
        {
            if (businessId == Guid.Empty)
                return BadRequest(new { success = false, message = "Invalid businessId" });

            // Button = always fetch and upsert (ignore TTL; do not deactivate)
            var result = await _sync.SyncBusinessTemplatesAsync(
                businessId,
                force: true,onlyUpsert: true);

            return Ok(new { success = true, result });
        }

        [HttpGet("{businessId:guid}")]
        [Authorize]
        public async Task<IActionResult> List(
      Guid businessId,
      [FromQuery] string? q = null,
      [FromQuery] string? status = "APPROVED",
      [FromQuery] string? language = null,
      [FromQuery] string? provider = null)
        {
            var query = _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToUpperInvariant();     // stored uppercase
                query = query.Where(x => x.Status == s);
            }

            if (!string.IsNullOrWhiteSpace(language))
            {
                var lang = language.Trim();
                query = query.Where(x => x.LanguageCode == lang);
            }

            if (!string.IsNullOrWhiteSpace(provider))
            {
                var prov = provider.Trim().ToUpperInvariant(); // stored uppercase
                query = query.Where(x => x.Provider == prov);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x =>
                    x.Name.Contains(term) ||
                    (x.Body != null && x.Body.Contains(term)));
            }

            var items = await query
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Name,
                    x.LanguageCode,
                    x.Status,
                    x.Category,
                    BodyVarCount = x.BodyVarCount, // <-- replaces PlaceholderCount
                    x.UrlButtons
                })
                .ToListAsync();

            return Ok(new { success = true, templates = items });
        }


        //[HttpGet("{businessId:guid}/{name}")]
        //[Authorize]


        //public async Task<IActionResult> GetOne(Guid businessId, string name, [FromQuery] string? language = null)
        //{
        //    var tpl = await _db.WhatsAppTemplates
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(x =>
        //            x.BusinessId == businessId
        //            && x.Name == name
        //            && (language == null || x.LanguageCode == language));

        //    if (tpl == null) return NotFound();

        //    // Prefer stored values; optionally refine via meta fetcher
        //    string headerKind = (tpl.HeaderKind ?? "none").Trim().ToLowerInvariant();
        //    bool requiresHeaderMediaUrl = tpl.RequiresMediaHeader
        //                                  || headerKind is "image" or "video" or "document";

        //    try
        //    {
        //        // If you still want live verification, keep this; otherwise you can remove the try/catch block
        //        var meta = await _fetcher.GetTemplateMetaAsync(
        //            businessId: businessId,
        //            templateName: tpl.Name,
        //            language: tpl.LanguageCode,
        //            provider: null);

        //        var ht = meta?.HeaderType?.Trim().ToUpperInvariant();
        //        if (!string.IsNullOrEmpty(ht))
        //        {
        //            headerKind = ht switch
        //            {
        //                "IMAGE" => "image",
        //                "VIDEO" => "video",
        //                "DOCUMENT" => "document",
        //                "TEXT" => "text",
        //                _ => headerKind
        //            };
        //            requiresHeaderMediaUrl = headerKind is "image" or "video" or "document";
        //        }
        //    }
        //    catch
        //    {
        //        // fall back to stored fields (already set above)
        //    }

        //    return Ok(new
        //    {
        //        tpl.Name,
        //        tpl.LanguageCode,
        //        tpl.Status,
        //        tpl.Category,
        //        tpl.Body,
        //        BodyVarCount = tpl.BodyVarCount,   // <- replaces old PlaceholderCount
        //        tpl.UrlButtons,
        //        headerKind,
        //        requiresHeaderMediaUrl
        //    });
        //}
        [HttpGet("{businessId:guid}/{name}")]
        [Authorize]
        public async Task<IActionResult> GetOne(Guid businessId, string name, [FromQuery] string? language = null)
        {
            var tpl = await _db.WhatsAppTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.BusinessId == businessId &&
                    x.Name == name &&
                    (language == null || x.LanguageCode == language));

            if (tpl == null) return NotFound();

            // ————— header info (keep your current behavior)
            string headerKind = (tpl.HeaderKind ?? "none").Trim().ToLowerInvariant();
            bool requiresHeaderMediaUrl = tpl.RequiresMediaHeader
                                          || headerKind is "image" or "video" or "document";
            try
            {
                var meta = await _fetcher.GetTemplateMetaAsync(
                    businessId: businessId,
                    templateName: tpl.Name,
                    language: tpl.LanguageCode,
                    provider: null);

                var ht = meta?.HeaderType?.Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(ht))
                {
                    headerKind = ht switch
                    {
                        "IMAGE" => "image",
                        "VIDEO" => "video",
                        "DOCUMENT" => "document",
                        "TEXT" => "text",
                        _ => headerKind
                    };
                    requiresHeaderMediaUrl = headerKind is "image" or "video" or "document";
                }
            }
            catch { /* fall back to stored */ }

            // ————— build a normalized buttons array from RawJson
            var buttons = new List<object>();
            try
            {
                // RawJson was saved during sync; shape matches provider response.
                // We'll look for components[].type == "BUTTONS".
                var root = Newtonsoft.Json.Linq.JObject.Parse(string.IsNullOrWhiteSpace(tpl.RawJson) ? "{}" : tpl.RawJson);
                var components = root.SelectToken("components") ?? root.SelectToken("data.components") ?? root.SelectToken("template.components");

                if (components is Newtonsoft.Json.Linq.JArray arr)
                {
                    foreach (var c in arr)
                    {
                        var type = c?["type"]?.ToString()?.ToUpperInvariant();
                        if (type != "BUTTONS") continue;

                        var btns = c["buttons"] as Newtonsoft.Json.Linq.JArray;
                        if (btns == null) continue;

                        int idx = 0;
                        foreach (var b in btns)
                        {
                            var btnType = b?["type"]?.ToString()?.ToUpperInvariant() ?? "";
                            var text = b?["text"]?.ToString() ?? "";
                            // default fields
                            string subType = btnType switch
                            {
                                "URL" => "url",
                                "PHONE_NUMBER" => "voice_call",
                                "QUICK_REPLY" => "quick_reply",
                                "COPY_CODE" => "copy_code",
                                "CATALOG" => "catalog",
                                "FLOW" => "flow",
                                "REMINDER" => "reminder",
                                "ORDER_DETAILS" => "order_details",
                                _ => "unknown"
                            };

                            // capture param (for dynamic URL / phone / coupon / flow)
                            string? param =
                                b?["url"]?.ToString()
                                ?? b?["phone_number"]?.ToString()
                                ?? b?["coupon_code"]?.ToString()
                                ?? b?["flow_id"]?.ToString();

                            buttons.Add(new
                            {
                                text = text,
                                type = btnType,        // original provider type (UPPERCASE)
                                subType = subType,     // normalized (lowercase)
                                index = (int?)(b?["index"]?.ToObject<int?>() ?? idx),
                                parameterValue = param // may be null for quick replies
                            });

                            idx++;
                        }
                    }
                }
            }
            catch
            {
                // ignore parse errors; just return other fields
            }

            return Ok(new
            {
                tpl.Name,
                LanguageCode = tpl.LanguageCode,
                tpl.Status,
                tpl.Category,
                Body = tpl.Body,
                BodyVarCount = tpl.BodyVarCount, // new field you already use in List
                UrlButtons = tpl.UrlButtons,     // keep legacy field (indexes of URL buttons)
                headerKind,
                requiresHeaderMediaUrl,
                buttons                           // 👈 NEW: full set including quick replies
            });
        }

    }
}
