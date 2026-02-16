using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Globalization;
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

        private static DateTime? TryReadMetaApprovedAtUtc(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return null;

            try
            {
                var root = ParsePossiblyStringifiedJson(rawJson);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "last_updated_time",
                    "lastUpdatedTime",
                    "updated_time",
                    "approved_time",
                    "approvedAt",
                    "approved_at"
                };

                var token = FindFirstTimestampToken(root, names);
                if (token == null || token.Type == Newtonsoft.Json.Linq.JTokenType.Null)
                    return null;

                var parsed = TryParseProviderTimestampToUtc(token);
                if (parsed == null) return null;

                // Approved/submitted timestamps should not be in the future (tiny skew allowed).
                if (parsed.Value > DateTime.UtcNow.AddMinutes(5)) return null;
                return parsed.Value;
            }
            catch
            {
                // Keep API resilient to malformed provider payloads.
            }

            return null;
        }

        private static Newtonsoft.Json.Linq.JToken ParsePossiblyStringifiedJson(string rawJson)
        {
            var token = Newtonsoft.Json.Linq.JToken.Parse(rawJson);

            // Some rows can be double-encoded (JSON object serialized as a JSON string).
            for (var i = 0; i < 2 && token.Type == Newtonsoft.Json.Linq.JTokenType.String; i++)
            {
                var inner = token.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(inner)) break;
                if (!(inner.StartsWith("{") || inner.StartsWith("["))) break;
                token = Newtonsoft.Json.Linq.JToken.Parse(inner);
            }

            return token;
        }

        private static Newtonsoft.Json.Linq.JToken? FindFirstTimestampToken(
            Newtonsoft.Json.Linq.JToken token,
            HashSet<string> names)
        {
            if (token is Newtonsoft.Json.Linq.JObject obj)
            {
                foreach (var p in obj.Properties())
                {
                    if (names.Contains(p.Name))
                        return p.Value;
                }

                foreach (var p in obj.Properties())
                {
                    var nested = FindFirstTimestampToken(p.Value, names);
                    if (nested != null) return nested;
                }
            }
            else if (token is Newtonsoft.Json.Linq.JArray arr)
            {
                foreach (var item in arr)
                {
                    var nested = FindFirstTimestampToken(item, names);
                    if (nested != null) return nested;
                }
            }

            return null;
        }

        private static DateTime? TryParseProviderTimestampToUtc(Newtonsoft.Json.Linq.JToken token)
        {
            // Meta can return ISO datetime or unix epoch (seconds / milliseconds).
            if (token.Type is Newtonsoft.Json.Linq.JTokenType.Integer or Newtonsoft.Json.Linq.JTokenType.Float)
            {
                if (long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                {
                    return n >= 1_000_000_000_000
                        ? DateTimeOffset.FromUnixTimeMilliseconds(n).UtcDateTime
                        : DateTimeOffset.FromUnixTimeSeconds(n).UtcDateTime;
                }
                return null;
            }

            // If JSON parser already typed it as Date, avoid culture-based string roundtrips.
            if (token.Type == Newtonsoft.Json.Linq.JTokenType.Date)
            {
                var asDto = token.ToObject<DateTimeOffset?>();
                if (asDto.HasValue) return asDto.Value.UtcDateTime;

                var asDt = token.ToObject<DateTime?>();
                if (asDt.HasValue)
                {
                    return asDt.Value.Kind switch
                    {
                        DateTimeKind.Utc => asDt.Value,
                        DateTimeKind.Local => asDt.Value.ToUniversalTime(),
                        _ => DateTime.SpecifyKind(asDt.Value, DateTimeKind.Utc)
                    };
                }
            }

            var raw = token.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch) && epoch > 0)
            {
                return epoch >= 1_000_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime
                    : DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            }

            if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
            {
                return dto.UtcDateTime;
            }

            return null;
        }

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

            var submittedTemplateNames = await _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId)
                .Select(x => x.Name.ToLower())
                .Distinct()
                .ToListAsync();

            var draftCount = await (
                from d in _db.TemplateDrafts.AsNoTracking()
                join v in _db.TemplateDraftVariants.AsNoTracking() 
                  on new { Did = d.Id, Lang = d.DefaultLanguage } equals new { Did = v.TemplateDraftId, Lang = v.Language } into g
                from v in g.DefaultIfEmpty()
                where d.BusinessId == businessId 
                   && d.SubmittedAt == null
                   && !submittedTemplateNames.Contains(d.Key)
                   // 🗑️ Filter out "empty" drafts (Untitled + No Content)
                   && (!d.Key.StartsWith("untitled_") || (v != null && (!string.IsNullOrWhiteSpace(v.BodyText) || v.HeaderType != "NONE")))
                select d.Id
            ).CountAsync();

            var libraryCount = await _db.TemplateLibraryItems
                .AsNoTracking()
                .CountAsync();

            return Ok(new
            {
                success = true,
                approved = stats.FirstOrDefault(s => s.Status == "APPROVED")?.Count ?? 0,
                pending = stats.Where(s => s.Status == "PENDING" || s.Status == "PENDING_APPROVAL" || s.Status == "PENDING_REVIEW" || s.Status == "IN_REVIEW").Sum(s => s.Count),
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
            [FromQuery] string sortKey = "approvedAt",
            [FromQuery] string sortDir = "desc")
        {
            var requestedStatus = (status ?? string.Empty).Trim().ToUpperInvariant();
            var isPendingBucket = requestedStatus == "PENDING";

            var query = _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive);

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                var s = status.Trim().ToUpperInvariant();
                if (s == "PENDING")
                {
                    query = query.Where(x => x.Status == "PENDING" || x.Status == "PENDING_APPROVAL" || x.Status == "PENDING_REVIEW" || x.Status == "IN_REVIEW");
                }
                else
                {
                    query = query.Where(x => x.Status == s);
                }
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

            bool isAsc = sortDir?.ToLowerInvariant() == "asc";
            var sortKeyLower = (sortKey ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(sortKeyLower))
                sortKeyLower = "approvedat";

            // Backward-compat: if old clients still request updatedAt on non-pending views,
            // keep approved-page behavior by sorting on provider approved timestamp.
            if (!isPendingBucket && sortKeyLower == "updatedat")
                sortKeyLower = "approvedat";

            // Materialize filtered set, then sort in-memory so we can sort by derived ApprovedAt too.
            var rows = await query
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
                    x.LastSyncedAt,
                    x.RawJson
                })
                .ToListAsync();

            var mappedItems = rows.Select(x => new
            {
                x.Name,
                x.LanguageCode,
                x.Status,
                x.Category,
                x.BodyPreview,
                x.BodyVarCount,
                x.HeaderKind,
                x.RequiresMediaHeader,
                x.CreatedAt,
                x.UrlButtons,
                x.UpdatedAt,
                x.LastSyncedAt,
                ApprovedAt = (DateTime?)(TryReadMetaApprovedAtUtc(x.RawJson) ?? x.UpdatedAt)
            }).ToList();

            var orderedItems = sortKeyLower switch
            {
                "name" => isAsc ? mappedItems.OrderBy(x => x.Name ?? string.Empty) : mappedItems.OrderByDescending(x => x.Name ?? string.Empty),
                "category" => isAsc ? mappedItems.OrderBy(x => x.Category ?? string.Empty) : mappedItems.OrderByDescending(x => x.Category ?? string.Empty),
                "language" => isAsc ? mappedItems.OrderBy(x => x.LanguageCode ?? string.Empty) : mappedItems.OrderByDescending(x => x.LanguageCode ?? string.Empty),
                "status" => isAsc ? mappedItems.OrderBy(x => x.Status ?? string.Empty) : mappedItems.OrderByDescending(x => x.Status ?? string.Empty),
                "createdat" => isAsc ? mappedItems.OrderBy(x => x.CreatedAt) : mappedItems.OrderByDescending(x => x.CreatedAt),
                "approvedat" => isAsc
                    ? mappedItems.OrderBy(x => x.ApprovedAt ?? DateTime.MaxValue)
                        .ThenBy(x => x.Name ?? string.Empty)
                    : mappedItems.OrderByDescending(x => x.ApprovedAt ?? DateTime.MinValue)
                        .ThenBy(x => x.Name ?? string.Empty),
                "updatedat" => isAsc ? mappedItems.OrderBy(x => x.UpdatedAt) : mappedItems.OrderByDescending(x => x.UpdatedAt),
                _ => mappedItems.OrderByDescending(x => x.ApprovedAt ?? DateTime.MinValue)
                    .ThenBy(x => x.Name ?? string.Empty)
            };

            var totalCount = mappedItems.Count;
            var pagedItems = orderedItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                success = true,
                templates = pagedItems,
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

            // Fallback: If Pending (optimistic) and no buttons found in RawJson, try to load from Draft
            if (buttons.Count == 0 && (tpl.Status == "PENDING" || tpl.Status == "PENDING_APPROVAL" || tpl.Status == "IN_REVIEW"))
            {
               var draft = await _db.TemplateDrafts
                   .AsNoTracking()
                   .Where(d => d.BusinessId == businessId && d.Key == tpl.Name)
                   .OrderByDescending(d => d.UpdatedAt)
                   .FirstOrDefaultAsync();
               
               if (draft != null)
               {
                   var variant = await _db.TemplateDraftVariants
                       .AsNoTracking()
                       .FirstOrDefaultAsync(v => v.TemplateDraftId == draft.Id && v.Language == tpl.LanguageCode);
                       
                   if (variant != null && !string.IsNullOrEmpty(variant.ButtonsJson))
                   {
                        try {
                            var dtos = System.Text.Json.JsonSerializer.Deserialize<List<xbytechat.api.Features.TemplateModule.DTOs.ButtonDto>>(variant.ButtonsJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                            if (dtos != null) 
                            {
                                int idx = 0;
                                foreach(var b in dtos) {
                                     string subType = b.Type.ToUpperInvariant() switch
                                    {
                                        "URL" => "url",
                                        "PHONE" => "voice_call",
                                        "PHONE_NUMBER" => "voice_call",
                                        "QUICK_REPLY" => "quick_reply",
                                        "COPY_CODE" => "copy_code",
                                        _ => "unknown"
                                    };
                                    
                                    string? param = b.Url ?? b.Phone;

                                    buttons.Add(new {
                                        text = b.Text,
                                        type = b.Type.ToUpperInvariant() == "PHONE" ? "PHONE_NUMBER" : b.Type.ToUpperInvariant(),
                                        subType,
                                        index = (int?)idx++,
                                        parameterValue = param
                                    });
                                }
                            }
                        } catch {}
                   }
               }
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
