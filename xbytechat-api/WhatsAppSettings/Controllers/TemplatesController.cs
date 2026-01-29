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

        [HttpGet("summary/{businessId:guid}")]
        [Authorize]
        public async Task<IActionResult> Summary(Guid businessId)
        {
            var stats = await _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive)
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var draftCount = await _db.TemplateDrafts
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId)
                .CountAsync();

            var libraryCount = await _db.TemplateLibraryItems
                .AsNoTracking()
                .CountAsync();

            return Ok(new
            {
                success = true,
                approved = stats.FirstOrDefault(s => s.Status == "APPROVED")?.Count ?? 0,
                pending = stats.FirstOrDefault(s => s.Status == "PENDING" || s.Status == "PENDING_APPROVAL")?.Count ?? 0,
                rejected = stats.FirstOrDefault(s => s.Status == "REJECTED")?.Count ?? 0,
                drafts = draftCount,
                library = libraryCount
            });
        }

        // Sync Templates
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
            [FromQuery] string? provider = null,
            [FromQuery] string? category = null,
            [FromQuery] string? media = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortKey = "updatedAt",
            [FromQuery] string sortDir = "desc")
        {
            var query = _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive);

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                var s = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.Status == s);
            }

            if (!string.IsNullOrWhiteSpace(language))
            {
                var lang = language.Trim();
                query = query.Where(x => x.LanguageCode == lang);
            }

            if (!string.IsNullOrWhiteSpace(provider))
            {
                var prov = provider.Trim().ToUpperInvariant();
                query = query.Where(x => x.Provider == prov);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var cat = category.Trim().ToUpperInvariant();
                query = query.Where(x => x.Category == cat);
            }

            // Media filter (header kind)
            // Supported values: all|text|image|video|document|pdf
            // Note: WhatsAppTemplate.HeaderKind is stored canonical lowercase (none/text/image/video/document/location).
            if (!string.IsNullOrWhiteSpace(media) && !media.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                var m = media.Trim().ToLowerInvariant();
                if (m == "pdf") m = "document";

                query = m switch
                {
                    "image" => query.Where(x => x.HeaderKind == "image"),
                    "video" => query.Where(x => x.HeaderKind == "video"),
                    "document" => query.Where(x => x.HeaderKind == "document"),
                    "text" => query.Where(x => !x.RequiresMediaHeader && (x.HeaderKind == "none" || x.HeaderKind == "text")),
                    _ => query
                };
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x =>
                    x.Name.Contains(term) ||
                    (x.Body != null && x.Body.Contains(term)));
            }

            // Sorting
            bool isAsc = sortDir?.ToLowerInvariant() == "asc";
            var sortKeyLower = sortKey?.ToLowerInvariant();

            query = sortKeyLower switch
            {
                "name" => isAsc ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
                "category" => isAsc ? query.OrderBy(x => x.Category) : query.OrderByDescending(x => x.Category),
                "language" => isAsc ? query.OrderBy(x => x.LanguageCode) : query.OrderByDescending(x => x.LanguageCode),
                "status" => isAsc ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status),
                "createdat" => isAsc ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                "updatedat" => isAsc ? query.OrderBy(x => x.UpdatedAt) : query.OrderByDescending(x => x.UpdatedAt),
                _ => query.OrderByDescending(x => x.UpdatedAt)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Name,
                    x.LanguageCode,
                    x.Status,
                    x.Category,
                    x.BodyPreview,
                    BodyVarCount = x.BodyVarCount,
                    x.HeaderKind,
                    x.RequiresMediaHeader,
                    x.CreatedAt,
                    x.UrlButtons,
                    x.UpdatedAt,
                    x.LastSyncedAt
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                templates = items,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
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
