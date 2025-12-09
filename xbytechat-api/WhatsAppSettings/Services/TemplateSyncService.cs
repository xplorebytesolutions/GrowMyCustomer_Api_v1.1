using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat_api.WhatsAppSettings.Models;
using xbytechat.api.WhatsAppSettings.Providers;
using xbytechat.api.WhatsAppSettings.Abstractions;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.WhatsAppSettings.Helpers; // canonical parser
using Microsoft.Extensions.Logging;
using xbytechat.api.WhatsAppSettings.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Polly.Caching;
using Serilog;
using System.Buffers;
using System.Text.RegularExpressions;

public sealed class TemplateSyncService : ITemplateSyncService
{
    private readonly AppDbContext _db;
    private readonly MetaTemplateCatalogProvider _meta;
    private readonly PinnacleTemplateCatalogProvider _pinnacle;
    private readonly ILogger<TemplateSyncService> _log;

    private static readonly TimeSpan TTL = TimeSpan.FromHours(12);

    public TemplateSyncService(
        AppDbContext db,
        MetaTemplateCatalogProvider meta,
        PinnacleTemplateCatalogProvider pinnacle,
        ILogger<TemplateSyncService> log)
    {
        _db = db; _meta = meta; _pinnacle = pinnacle; _log = log;
    }

    //public async Task<TemplateSyncResult> SyncBusinessTemplatesAsync(Guid businessId, bool force = false, CancellationToken ct = default)
    //{
    //    var setting = await _db.WhatsAppSettings
    //        .AsNoTracking()
    //        .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.IsActive, ct)
    //        ?? throw new InvalidOperationException("Active WhatsApp settings not found.");

    //    var now = DateTime.UtcNow;

    //    if (!force)
    //    {
    //        var recent = await _db.WhatsAppTemplates
    //            .AsNoTracking()
    //            .Where(t => t.BusinessId == businessId)
    //            .OrderByDescending(t => t.LastSyncedAt)
    //            .Select(t => t.LastSyncedAt)
    //            .FirstOrDefaultAsync(ct);

    //        if (recent != default && now - recent < TTL)
    //        {
    //            _log.LogInformation("⏭️ Skipping sync for {BusinessId}; TTL not expired.", businessId);
    //            return new TemplateSyncResult(0, 0, 0, recent);
    //        }
    //    }

    //    // Canonical provider (UPPER)
    //    var providerKey = ((setting.Provider ?? "META_CLOUD").Trim()).ToUpperInvariant();

    //    IReadOnlyList<TemplateCatalogItem> incoming = providerKey switch
    //    {
    //        "META_CLOUD" => await _meta.ListMetaAsync(setting, ct),
    //        "PINNACLE" => await _pinnacle.ListPinnacleAsync(setting, ct),
    //        _ => Array.Empty<TemplateCatalogItem>()
    //    };
    //    incoming ??= Array.Empty<TemplateCatalogItem>();

    //    // Preload existing provider rows for this business
    //    var existing = await _db.WhatsAppTemplates
    //        .Where(t => t.BusinessId == businessId && t.Provider == providerKey)
    //        .ToListAsync(ct);

    //    // Fast lookup maps
    //    var byTemplateId = existing
    //        .Where(e => !string.IsNullOrWhiteSpace(e.TemplateId))
    //        .ToDictionary(e => e.TemplateId!, e => e, StringComparer.Ordinal);

    //    //static string NLKey(string name, string? lang) => $"{name}::{(lang ?? "").Trim().ToLowerInvariant()}";
    //    static string NLKey(string? name, string? lang)
    //=> $"{(name ?? "").Trim().ToLowerInvariant()}::{(lang ?? "").Trim().ToLowerInvariant()}";
    //    var byNameLang = existing.ToDictionary(
    //        e => NLKey(e.Name, e.LanguageCode),
    //        e => e,
    //        StringComparer.Ordinal);

    //    int added = 0, updated = 0, unchanged = 0;

    //    var seenTemplateIds = new HashSet<string>(StringComparer.Ordinal);
    //    var seenNLKeys = new HashSet<string>(StringComparer.Ordinal);

    //    // Helpers
    //    static string NK(string? k) => string.IsNullOrWhiteSpace(k) ? "none" : k.Trim().ToLowerInvariant();
    //    static bool IsIdentifier(string? s) => !string.IsNullOrWhiteSpace(s) && Regex.IsMatch(s!, @"^[A-Za-z_][A-Za-z0-9_]*$");

    //    foreach (var it in incoming)
    //    {
    //        ct.ThrowIfCancellationRequested();

    //        var extId = it.ExternalId?.Trim();
    //        var lang = string.IsNullOrWhiteSpace(it.Language) ? "en_US" : it.Language.Trim();
    //        var nlKey = NLKey(it.Name, lang);

    //        seenNLKeys.Add(nlKey);
    //        if (!string.IsNullOrWhiteSpace(extId)) seenTemplateIds.Add(extId!);

    //        // —— Parse/summarize JSON into our canonical fields
    //        var dto = TemplateJsonHelper.SummarizeDetailed(it.RawJson, it.Body);

    //        var headerKindNorm = NK(dto.HeaderKind);
    //        var headerTextToPersist = headerKindNorm == "text"
    //            ? (string.IsNullOrWhiteSpace(dto.HeaderText) ? null : dto.HeaderText!.Trim())
    //            : null;

    //        var paramFormat = (dto.ParameterFormat ?? "UNKNOWN").Trim().ToUpperInvariant();

    //        // Keep only real placeholders (drop "{{}}"). Count by location.
    //        var occAll = (dto.Placeholders ?? new List<PlaceholderOccurrence>()).ToList();
    //        var occFiltered = occAll.Where(o =>
    //        {
    //            var raw = o.Raw?.Trim();
    //            if (string.Equals(raw, "{{}}", StringComparison.Ordinal)) return false;

    //            if (paramFormat == "POSITIONAL") return o.Index is >= 1;
    //            if (paramFormat == "NAMED") return IsIdentifier(o.Name);

    //            return false;
    //        }).ToList();

    //        int headerVars = occFiltered.Count(p => p.Location == PlaceholderLocation.Header);
    //        int bodyVars = occFiltered.Count(p => p.Location == PlaceholderLocation.Body);
    //        int totalVars = occFiltered.Count; // header + body + buttons (filtered)

    //        var combinedBody = dto.CombinedPreviewBody ?? string.Empty;
    //        var bodyPreview = combinedBody.Length > 240 ? combinedBody[..240] : combinedBody;

    //        bool requiresMediaHeader = headerKindNorm is "image" or "video" or "document";

    //        // Named parameter keys (for NAMED format)
    //        var namedKeys = occFiltered
    //            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
    //            .Select(p => p.Name!.Trim())
    //            .Where(IsIdentifier)
    //            .Distinct(StringComparer.OrdinalIgnoreCase)
    //            .ToArray();
    //        string? namedKeysJson = namedKeys.Length > 0 ? JsonConvert.SerializeObject(namedKeys) : null;

    //        // Placeholder map (provider-specific): keep null unless you compute elsewhere
    //        string? placeholderMapJson = null;

    //        // Buttons summary (prefer explicit from provider)
    //        // Buttons summary (prefer explicit from provider)
    //        string? urlButtonsJson = null;
    //        int quickReplyCount = 0;
    //        bool hasPhoneButton = false;

    //        if (it.Buttons is { Count: > 0 })
    //        {
    //            // Persist only what's needed downstream: the URL button positions.
    //            // Shape: [{ "index": 0 }, { "index": 1 }, ...]
    //            var urlButtons = new List<object>(capacity: it.Buttons.Count);

    //            foreach (var b in it.Buttons)
    //            {
    //                var sub = (b.SubType ?? "").Trim().ToLowerInvariant();

    //                if (sub == "quick_reply") quickReplyCount++;
    //                if (sub == "phone_number") hasPhoneButton = true;

    //                if (sub == "url")
    //                {
    //                    urlButtons.Add(new
    //                    {
    //                        index = b.Index
    //                        // no b.Parameters; not present on ButtonMetadataDto
    //                    });
    //                }
    //            }

    //            urlButtonsJson = urlButtons.Count > 0 ? JsonConvert.SerializeObject(urlButtons) : null;
    //        }


    //        var status = string.IsNullOrWhiteSpace(it.Status) ? "APPROVED" : it.Status!.Trim().ToUpperInvariant();

    //        // —— Locate existing row (by TemplateId first, else Name+Lang)
    //        WhatsAppTemplate? row = null;

    //        if (!string.IsNullOrWhiteSpace(extId) && byTemplateId.TryGetValue(extId!, out var foundById))
    //        {
    //            row = foundById;
    //        }
    //        else if (byNameLang.TryGetValue(nlKey, out var foundByNL))
    //        {
    //            row = foundByNL;

    //            // backfill TemplateId if we now have it
    //            if (!string.IsNullOrWhiteSpace(extId) && string.IsNullOrWhiteSpace(row.TemplateId))
    //            {
    //                row.TemplateId = extId;
    //                updated++;
    //            }
    //        }

    //        if (row is null)
    //        {
    //            // INSERT
    //            var newRow = new WhatsAppTemplate
    //            {
    //                Id = Guid.NewGuid(),
    //                BusinessId = businessId,
    //                Provider = providerKey,
    //                TemplateId = extId,
    //                Name = it.Name,
    //                LanguageCode = lang,
    //                Status = status,
    //                Category = it.Category,
    //                SubCategory = null,

    //                RawJson = it.RawJson ?? "{}",

    //                ParameterFormat = paramFormat,
    //                HeaderKind = headerKindNorm,
    //                HeaderText = headerTextToPersist,
    //                Body = combinedBody,
    //                BodyPreview = bodyPreview,

    //                RequiresMediaHeader = requiresMediaHeader,

    //                BodyVarCount = bodyVars,
    //                HeaderTextVarCount = headerVars,
    //                TotalTextParamCount = totalVars,

    //                UrlButtons = urlButtonsJson,
    //                QuickReplyCount = quickReplyCount,
    //                HasPhoneButton = hasPhoneButton,

    //                NamedParamKeys = namedKeysJson,
    //                PlaceholderMap = placeholderMapJson,

    //                IsActive = true,
    //                CreatedAt = now,
    //                UpdatedAt = now,
    //                LastSyncedAt = now
    //            };

    //            _db.WhatsAppTemplates.Add(newRow);
    //            added++;

    //            if (!string.IsNullOrWhiteSpace(extId)) byTemplateId[extId!] = newRow;
    //            byNameLang[nlKey] = newRow;
    //        }
    //        else
    //        {
    //            // UPDATE (branch-predicated, no ref locals)
    //            bool changed = false;

    //            if (!string.Equals(row.Status, status, StringComparison.Ordinal))
    //            { row.Status = status; changed = true; }

    //            if (!string.Equals(row.Category, it.Category, StringComparison.Ordinal))
    //            { row.Category = it.Category; changed = true; }

    //            var newRaw = it.RawJson ?? "{}";
    //            if (!string.Equals(row.RawJson, newRaw, StringComparison.Ordinal))
    //            { row.RawJson = newRaw; changed = true; }

    //            if (!string.Equals(row.ParameterFormat, paramFormat, StringComparison.Ordinal))
    //            { row.ParameterFormat = paramFormat; changed = true; }

    //            if (!string.Equals(row.HeaderKind, headerKindNorm, StringComparison.Ordinal))
    //            { row.HeaderKind = headerKindNorm; changed = true; }

    //            var existingHeaderText = string.IsNullOrWhiteSpace(row.HeaderText) ? null : row.HeaderText!.Trim();
    //            if (!string.Equals(existingHeaderText, headerTextToPersist, StringComparison.Ordinal))
    //            { row.HeaderText = headerTextToPersist; changed = true; }

    //            if (!string.Equals(row.Body ?? string.Empty, combinedBody, StringComparison.Ordinal))
    //            { row.Body = combinedBody; changed = true; }

    //            if (!string.Equals(row.BodyPreview ?? string.Empty, bodyPreview ?? string.Empty, StringComparison.Ordinal))
    //            { row.BodyPreview = bodyPreview; changed = true; }

    //            if (row.RequiresMediaHeader != requiresMediaHeader)
    //            { row.RequiresMediaHeader = requiresMediaHeader; changed = true; }

    //            if (row.BodyVarCount != bodyVars) { row.BodyVarCount = bodyVars; changed = true; }
    //            if (row.HeaderTextVarCount != headerVars) { row.HeaderTextVarCount = headerVars; changed = true; }
    //            if (row.TotalTextParamCount != totalVars) { row.TotalTextParamCount = totalVars; changed = true; }

    //            if (!string.Equals(row.UrlButtons ?? "", urlButtonsJson ?? "", StringComparison.Ordinal))
    //            { row.UrlButtons = urlButtonsJson; changed = true; }

    //            if (row.QuickReplyCount != quickReplyCount) { row.QuickReplyCount = quickReplyCount; changed = true; }
    //            if (row.HasPhoneButton != hasPhoneButton) { row.HasPhoneButton = hasPhoneButton; changed = true; }

    //            if (!string.Equals(row.NamedParamKeys ?? "", namedKeysJson ?? "", StringComparison.Ordinal))
    //            { row.NamedParamKeys = namedKeysJson; changed = true; }

    //            if (!string.Equals(row.PlaceholderMap ?? "", placeholderMapJson ?? "", StringComparison.Ordinal))
    //            { row.PlaceholderMap = placeholderMapJson; changed = true; }

    //            row.IsActive = true;
    //            row.LastSyncedAt = now;

    //            if (changed) { row.UpdatedAt = now; updated++; } else { unchanged++; }
    //        }
    //    }

    //    // Deactivate templates that disappeared from provider (cheap set membership)
    //    if (incoming.Count > 0)
    //    {
    //        foreach (var e in existing)
    //        {
    //            bool stillThere =
    //                (!string.IsNullOrWhiteSpace(e.TemplateId) && seenTemplateIds.Contains(e.TemplateId)) ||
    //                seenNLKeys.Contains(NLKey(e.Name, e.LanguageCode));

    //            if (!stillThere && e.IsActive)
    //            {
    //                e.IsActive = false;
    //                e.LastSyncedAt = now;
    //                e.UpdatedAt = now;
    //                updated++;
    //            }
    //        }
    //    }

    //    await _db.SaveChangesAsync(ct);
    //    return new TemplateSyncResult(added, updated, unchanged, now);
    //}

    // NOTE: expects fields/services available in your class:
    // _db (AppDbContext), _log (ILogger), _meta, _pinnacle, TTL (TimeSpan)

    public async Task<TemplateSyncResult> SyncBusinessTemplatesAsync(
     Guid businessId,
     bool force = false,
     bool onlyUpsert = false, // NEW: when true, do not deactivate missing rows
     CancellationToken ct = default)
    {
        var setting = await _db.WhatsAppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.IsActive, ct)
            ?? throw new InvalidOperationException("Active WhatsApp settings not found.");

        var now = DateTime.UtcNow;

        // Canonical provider key (UPPER)
        var providerKey = ((setting.Provider ?? "META_CLOUD").Trim()).ToUpperInvariant();

        // TTL check is provider-scoped; will be bypassed by force:true from the button
        if (!force)
        {
            var recent = await _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(t => t.BusinessId == businessId && t.Provider.ToUpper() == providerKey)
                .OrderByDescending(t => t.LastSyncedAt)
                .Select(t => t.LastSyncedAt)
                .FirstOrDefaultAsync(ct);

            if (recent != default && now - recent < TTL)
            {
                _log.LogInformation("⏭️ Skipping template sync (business={BusinessId}, provider={Provider}) — TTL not expired. Last={Last}, TTL={TTLmins}m",
                    businessId, providerKey, recent, TTL.TotalMinutes);
                return new TemplateSyncResult(0, 0, 0, recent);
            }
        }

        // Fetch from provider
        IReadOnlyList<TemplateCatalogItem> incoming = providerKey switch
        {
            "META_CLOUD" => await _meta.ListMetaAsync(setting, ct),
            "PINNACLE" => await _pinnacle.ListPinnacleAsync(setting, ct),
            _ => Array.Empty<TemplateCatalogItem>()
        };
        incoming ??= Array.Empty<TemplateCatalogItem>();

        _log.LogInformation("[TplSync] Provider={Provider} fetched={Count}", providerKey, incoming.Count);

        // Load existing rows for this provider (normalize Provider case)
        var existing = await _db.WhatsAppTemplates
            .Where(t => t.BusinessId == businessId && t.Provider.ToUpper() == providerKey)
            .ToListAsync(ct);

        // Fast lookup maps
        var byTemplateId = existing
            .Where(e => !string.IsNullOrWhiteSpace(e.TemplateId))
            .ToDictionary(e => e.TemplateId!, e => e, StringComparer.Ordinal);

        static string NLKey(string? name, string? lang)
            => $"{(name ?? "").Trim().ToLowerInvariant()}::{(lang ?? "").Trim().ToLowerInvariant()}";

        var byNameLang = existing.ToDictionary(
            e => NLKey(e.Name, e.LanguageCode),
            e => e,
            StringComparer.Ordinal);

        int added = 0, updated = 0, unchanged = 0;

        var seenTemplateIds = new HashSet<string>(StringComparer.Ordinal);
        var seenNLKeys = new HashSet<string>(StringComparer.Ordinal);

        // Helpers
        static string NK(string? k) => string.IsNullOrWhiteSpace(k) ? "none" : k.Trim().ToLowerInvariant();
        static bool IsIdentifier(string? s) => !string.IsNullOrWhiteSpace(s) && Regex.IsMatch(s!, @"^[A-Za-z_][A-Za-z0-9_]*$");

        foreach (var it in incoming)
        {
            ct.ThrowIfCancellationRequested();

            var extId = it.ExternalId?.Trim();
            var lang = string.IsNullOrWhiteSpace(it.Language) ? "en_US" : it.Language.Trim();
            var nlKey = NLKey(it.Name, lang);

            seenNLKeys.Add(nlKey);
            if (!string.IsNullOrWhiteSpace(extId)) seenTemplateIds.Add(extId!);

            // Parse & summarize provider JSON
            var dto = TemplateJsonHelper.SummarizeDetailed(it.RawJson, it.Body);

            var headerKindNorm = NK(dto.HeaderKind);
            var headerTextToPersist = headerKindNorm == "text"
                ? (string.IsNullOrWhiteSpace(dto.HeaderText) ? null : dto.HeaderText!.Trim())
                : null;

            var paramFormat = (dto.ParameterFormat ?? "UNKNOWN").Trim().ToUpperInvariant();

            // Real placeholders only
            var occAll = (dto.Placeholders ?? new List<PlaceholderOccurrence>()).ToList();
            var occFiltered = occAll.Where(o =>
            {
                var raw = o.Raw?.Trim();
                if (string.Equals(raw, "{{}}", StringComparison.Ordinal)) return false;

                if (paramFormat == "POSITIONAL") return o.Index is >= 1;
                if (paramFormat == "NAMED") return IsIdentifier(o.Name);
                return false;
            }).ToList();

            int headerVars = occFiltered.Count(p => p.Location == PlaceholderLocation.Header);
            int bodyVars = occFiltered.Count(p => p.Location == PlaceholderLocation.Body);
            int totalVars = occFiltered.Count;

            var combinedBody = dto.CombinedPreviewBody ?? string.Empty;
            var bodyPreview = combinedBody.Length > 240 ? combinedBody[..240] : combinedBody;

            bool requiresMediaHeader = headerKindNorm is "image" or "video" or "document";

            // Named param keys
            var namedKeys = occFiltered
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name!.Trim())
                .Where(IsIdentifier)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string? namedKeysJson = namedKeys.Length > 0 ? JsonConvert.SerializeObject(namedKeys) : null;

            // Buttons summary
            string? urlButtonsJson = null;
            int quickReplyCount = 0;
            bool hasPhoneButton = false;

            if (it.Buttons is { Count: > 0 })
            {
                var urlButtons = new List<object>(capacity: it.Buttons.Count);

                foreach (var b in it.Buttons)
                {
                    var sub = (b.SubType ?? "").Trim().ToLowerInvariant();

                    if (sub == "quick_reply") quickReplyCount++;
                    if (sub == "voice_call") hasPhoneButton = true;

                    if (sub == "url")
                    {
                        urlButtons.Add(new { index = b.Index });
                    }
                }

                urlButtonsJson = urlButtons.Count > 0 ? JsonConvert.SerializeObject(urlButtons) : null;
            }

            var status = string.IsNullOrWhiteSpace(it.Status) ? "APPROVED" : it.Status!.Trim().ToUpperInvariant();

            // Locate existing (TemplateId first, else Name+Lang)
            WhatsAppTemplate? row = null;

            if (!string.IsNullOrWhiteSpace(extId) && byTemplateId.TryGetValue(extId!, out var foundById))
            {
                row = foundById;
            }
            else if (byNameLang.TryGetValue(nlKey, out var foundByNL))
            {
                row = foundByNL;

                // backfill TemplateId if present now
                if (!string.IsNullOrWhiteSpace(extId) && string.IsNullOrWhiteSpace(row.TemplateId))
                {
                    row.TemplateId = extId;
                    updated++;
                }
            }

            if (row is null)
            {
                // INSERT
                var newRow = new WhatsAppTemplate
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Provider = providerKey,
                    TemplateId = extId,
                    Name = it.Name,
                    LanguageCode = lang,
                    Status = status,
                    Category = it.Category,
                    SubCategory = null,

                    RawJson = it.RawJson ?? "{}",

                    ParameterFormat = paramFormat,
                    HeaderKind = headerKindNorm,
                    HeaderText = headerTextToPersist,
                    Body = combinedBody,
                    BodyPreview = bodyPreview,

                    RequiresMediaHeader = requiresMediaHeader,

                    BodyVarCount = bodyVars,
                    HeaderTextVarCount = headerVars,
                    TotalTextParamCount = totalVars,

                    UrlButtons = urlButtonsJson,
                    QuickReplyCount = quickReplyCount,
                    HasPhoneButton = hasPhoneButton,

                    NamedParamKeys = namedKeysJson,
                    PlaceholderMap = null,

                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastSyncedAt = now
                };

                _db.WhatsAppTemplates.Add(newRow);
                added++;

                if (!string.IsNullOrWhiteSpace(extId)) byTemplateId[extId!] = newRow;
                byNameLang[nlKey] = newRow;
            }
            else
            {
                // UPDATE
                bool changed = false;

                if (!string.Equals(row.Status, status, StringComparison.Ordinal))
                { row.Status = status; changed = true; }

                if (!string.Equals(row.Category, it.Category, StringComparison.Ordinal))
                { row.Category = it.Category; changed = true; }

                var newRaw = it.RawJson ?? "{}";
                if (!string.Equals(row.RawJson, newRaw, StringComparison.Ordinal))
                { row.RawJson = newRaw; changed = true; }

                if (!string.Equals(row.ParameterFormat, paramFormat, StringComparison.Ordinal))
                { row.ParameterFormat = paramFormat; changed = true; }

                if (!string.Equals(row.HeaderKind, headerKindNorm, StringComparison.Ordinal))
                { row.HeaderKind = headerKindNorm; changed = true; }

                var existingHeaderText = string.IsNullOrWhiteSpace(row.HeaderText) ? null : row.HeaderText!.Trim();
                if (!string.Equals(existingHeaderText, headerTextToPersist, StringComparison.Ordinal))
                { row.HeaderText = headerTextToPersist; changed = true; }

                if (!string.Equals(row.Body ?? string.Empty, combinedBody, StringComparison.Ordinal))
                { row.Body = combinedBody; changed = true; }

                if (!string.Equals(row.BodyPreview ?? string.Empty, bodyPreview ?? string.Empty, StringComparison.Ordinal))
                { row.BodyPreview = bodyPreview; changed = true; }

                if (row.RequiresMediaHeader != requiresMediaHeader)
                { row.RequiresMediaHeader = requiresMediaHeader; changed = true; }

                if (row.BodyVarCount != bodyVars) { row.BodyVarCount = bodyVars; changed = true; }
                if (row.HeaderTextVarCount != headerVars) { row.HeaderTextVarCount = headerVars; changed = true; }
                if (row.TotalTextParamCount != totalVars) { row.TotalTextParamCount = totalVars; changed = true; }

                if (!string.Equals(row.UrlButtons ?? "", urlButtonsJson ?? "", StringComparison.Ordinal))
                { row.UrlButtons = urlButtonsJson; changed = true; }

                if (row.QuickReplyCount != quickReplyCount) { row.QuickReplyCount = quickReplyCount; changed = true; }
                if (row.HasPhoneButton != hasPhoneButton) { row.HasPhoneButton = hasPhoneButton; changed = true; }

                if (!string.Equals(row.NamedParamKeys ?? "", namedKeysJson ?? "", StringComparison.Ordinal))
                { row.NamedParamKeys = namedKeysJson; changed = true; }

                // PlaceholderMap remains as-is unless you compute it elsewhere

                row.IsActive = true;
                row.LastSyncedAt = now;

                if (changed) { row.UpdatedAt = now; updated++; } else { unchanged++; }
            }
        }

        // ⛔️ Deactivation is skipped when onlyUpsert == true
        if (!onlyUpsert && incoming.Count > 0)
        {
            foreach (var e in existing)
            {
                bool stillThere =
                    (!string.IsNullOrWhiteSpace(e.TemplateId) && seenTemplateIds.Contains(e.TemplateId)) ||
                    seenNLKeys.Contains(NLKey(e.Name, e.LanguageCode));

                if (!stillThere && e.IsActive)
                {
                    e.IsActive = false;
                    e.LastSyncedAt = now;
                    e.UpdatedAt = now;
                    updated++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        _log.LogInformation("[TplSync] Done (business={BusinessId}, provider={Provider}) added={Added} updated={Updated} unchanged={Unchanged} onlyUpsert={OnlyUpsert}",
            businessId, providerKey, added, updated, unchanged, onlyUpsert);

        return new TemplateSyncResult(added, updated, unchanged, now);
    }


    private static (string HeaderKind, string CombinedBody, int PlaceholderCount)
                    ComputeFromRawOrFallback(TemplateCatalogItem it)
    {
        var (headerKind, combinedBody, placeholderCount) =
            TemplateJsonHelper.Summarize(it.RawJson, it.Body);

        return (string.IsNullOrWhiteSpace(headerKind) ? "none" : headerKind,
                combinedBody ?? string.Empty,
                placeholderCount);
    }

    ///
    // Add inside TemplateSyncService class
    private static (string headerKind, bool requiresMedia, string? headerText,
                   string parameterFormat, int bodyVars, int headerVars, int totalVars,
                   string? namedKeysJson, string? placeholderMapJson,
                   string? body, string? bodyPreview,
                   string? urlButtonsJson, int quickReplyCount, bool hasPhoneButton)
    SummarizeFromRawJson(string rawJson, string? bodyFallback)
    {
        // Uses your existing canonical JSON helper if present; otherwise lightweight parse.
        // Prefer TemplateJsonHelper if it exists in your project.
        try
        {
            // TemplateJsonHelper.Summarize should already compute combined details in your repo.
            // Replace this block with that call if available to keep logic single-sourced.
            // var s = TemplateJsonHelper.Summarize(rawJson, bodyFallback);
            // return (... map from s ...);

            var jo = Newtonsoft.Json.Linq.JObject.Parse(string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson);
            var components = (Newtonsoft.Json.Linq.JArray?)jo["components"] ?? new();
            string headerKind = "none";
            string? headerText = null;
            string parameterFormat = "UNKNOWN";

            // HEADER detection
            var header = components.FirstOrDefault(c =>
                string.Equals((string?)c?["type"], "HEADER", StringComparison.OrdinalIgnoreCase)) as Newtonsoft.Json.Linq.JObject;

            var headerFormat = ((string?)header?["format"])?.ToLowerInvariant();
            headerKind = headerFormat switch
            {
                "image" => "image",
                "video" => "video",
                "document" => "document",
                "text" => "text",
                "location" => "location",
                _ => "none"
            };
            if (string.Equals(headerKind, "text", StringComparison.OrdinalIgnoreCase))
                headerText = (string?)header?["text"];

            bool requiresMedia = headerKind is "image" or "video" or "document";

            // BODY
            var body = (string?)components.FirstOrDefault(c => string.Equals((string?)c?["type"], "BODY", StringComparison.OrdinalIgnoreCase))?["text"]
                       ?? bodyFallback ?? string.Empty;
            var bodyPreview = body?.Length > 240 ? body.Substring(0, 240) : body;

            // NAMED vs POSITIONAL slots (rough heuristic: if there are explicit named keys, mark NAMED)
            // Count positional: {{1}}, {{ 2 }}, etc. Count named blank tokens: {{}} as a slot marker.
            var positional = System.Text.RegularExpressions.Regex.Matches(body ?? "", @"\{\{\s*\d+\s*\}\}").Count;
            var namedBlank = System.Text.RegularExpressions.Regex.Matches(body ?? "", @"\{\{\s*\}\}").Count;

            // Collect named param keys if present in RAW JSON (Meta has examples under example/parameters/name)
            var namedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var comp in components.OfType<Newtonsoft.Json.Linq.JObject>())
            {
                var paramsArr = comp["example"]?["parameters"] as Newtonsoft.Json.Linq.JArray;
                if (paramsArr != null)
                {
                    foreach (var p in paramsArr.OfType<Newtonsoft.Json.Linq.JObject>())
                    {
                        var name = (string?)p["key"] ?? (string?)p["name"];
                        if (!string.IsNullOrWhiteSpace(name)) namedKeys.Add(name!);
                    }
                }
            }
            parameterFormat = namedKeys.Count > 0 ? "NAMED" : (positional > 0 ? "POSITIONAL" : "UNKNOWN");

            // Header text variable count (rough: treat {{}}/positional in headerText)
            var headerVars = 0;
            if (!string.IsNullOrEmpty(headerText))
            {
                headerVars += System.Text.RegularExpressions.Regex.Matches(headerText, @"\{\{\s*\d+\s*\}\}").Count;
                headerVars += System.Text.RegularExpressions.Regex.Matches(headerText, @"\{\{\s*\}\}").Count;
            }

            var bodyVars = positional + namedBlank;
            var totalVars = bodyVars + headerVars;

            // Buttons → collect url buttons + quick replies + phone button presence
            var buttons = components.FirstOrDefault(c => string.Equals((string?)c?["type"], "BUTTONS", StringComparison.OrdinalIgnoreCase)) as Newtonsoft.Json.Linq.JObject;
            var btnArr = buttons?["buttons"] as Newtonsoft.Json.Linq.JArray ?? new();

            var quickReplyCount = 0;
            var hasPhoneButton = false;
            var urlButtonsList = new List<Newtonsoft.Json.Linq.JObject>();

            foreach (var b in btnArr.OfType<Newtonsoft.Json.Linq.JObject>())
            {
                var sub = ((string?)b["sub_type"])?.ToLowerInvariant();
                if (sub == "quick_reply") quickReplyCount++;
                if (sub == "phone_number") hasPhoneButton = true;
                if (sub == "url")
                {
                    // retain minimal facsimile for UrlButtons (index + parameter template if any)
                    var index = (string?)b["index"] ?? "0";
                    var parameters = b["parameters"] as Newtonsoft.Json.Linq.JArray;
                    urlButtonsList.Add(new Newtonsoft.Json.Linq.JObject
                    {
                        ["index"] = index,
                        ["parameters"] = parameters ?? new Newtonsoft.Json.Linq.JArray()
                    });
                }
            }

            var urlButtonsJson = urlButtonsList.Count > 0
                ? Newtonsoft.Json.JsonConvert.SerializeObject(urlButtonsList)
                : null;

            var namedKeysJson = namedKeys.Count > 0
                ? Newtonsoft.Json.JsonConvert.SerializeObject(namedKeys.ToArray())
                : null;

            // Placeholder map is provider-specific; keep null unless you compute it elsewhere
            string? placeholderMapJson = null;

            return (headerKind, requiresMedia, headerText,
                    parameterFormat, bodyVars, headerVars, totalVars,
                    namedKeysJson, placeholderMapJson,
                    body, bodyPreview,
                    urlButtonsJson, quickReplyCount, hasPhoneButton);
        }
        catch
        {
            // very defensive fallback
            var body = bodyFallback ?? string.Empty;
            var bodyVars = System.Text.RegularExpressions.Regex.Matches(body, @"\{\{\s*\d+\s*\}\}").Count
                         + System.Text.RegularExpressions.Regex.Matches(body, @"\{\{\s*\}\}").Count;
            var preview = body.Length > 240 ? body.Substring(0, 240) : body;
            return ("none", false, null, "UNKNOWN", bodyVars, 0, bodyVars, null, null, body, preview, null, 0, false);
        }
    }

}
