using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Helpers;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Owns campaign CSV audience attachment lifecycle:
    /// - preserves attachment history (deactivate, never delete)
    /// - enforces "sent-lock" (409 once any send occurred)
    /// - rebuilds ONLY CSV-derived recipients (AudienceMemberId != null)
    /// </summary>
    public sealed class CampaignAudienceAttachmentService : ICampaignAudienceAttachmentService
    {
        private readonly AppDbContext _db;
        private readonly ICsvBatchService _csv;
        private readonly IVariableResolver _resolver;

        // Common phone header candidates (case-insensitive)
        private static readonly string[] PhoneHeaderCandidates =
        {
            "phone", "mobile", "whatsapp", "msisdn", "whatsapp_number", "contact", "contact_number"
        };

        public CampaignAudienceAttachmentService(AppDbContext db, ICsvBatchService csv, IVariableResolver resolver)
        {
            _db = db;
            _csv = csv;
            _resolver = resolver;
        }

        public async Task<CampaignAudienceDto> GetActiveAsync(Guid businessId, Guid campaignId, CancellationToken ct = default)
        {
            // Return an "empty" DTO rather than 404 so the UI can render "no attachment".
            var (exists, isLocked) = await GetCampaignExistenceAndLockAsync(businessId, campaignId, ct);
            if (!exists) throw new KeyNotFoundException("Campaign not found.");

            var active = await _db.CampaignAudienceAttachments
                .AsNoTracking()
                .Where(a => a.BusinessId == businessId && a.CampaignId == campaignId && a.IsActive)
                .Select(a => new
                {
                    a.Id,
                    a.AudienceId,
                    AudienceName = a.Audience.Name,
                    a.CsvBatchId,
                    a.FileName,
                    a.CreatedAt
                })
                .FirstOrDefaultAsync(ct);

            if (active == null)
                return new CampaignAudienceDto { IsLocked = isLocked };

            var memberCount = await _db.AudienceMembers
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.AudienceId == active.AudienceId && !m.IsDeleted)
                .CountAsync(ct);

            return new CampaignAudienceDto
            {
                AttachmentId = active.Id,
                AudienceId = active.AudienceId,
                AudienceName = active.AudienceName,
                CsvBatchId = active.CsvBatchId,
                FileName = active.FileName,
                CreatedAt = active.CreatedAt,
                MemberCount = memberCount,
                IsLocked = isLocked
            };
        }

        public async Task<IReadOnlyList<CampaignAudienceHistoryItemDto>> GetHistoryAsync(Guid businessId, Guid campaignId, CancellationToken ct = default)
        {
            var (exists, _) = await GetCampaignExistenceAndLockAsync(businessId, campaignId, ct);
            if (!exists) throw new KeyNotFoundException("Campaign not found.");

            return await _db.CampaignAudienceAttachments
                .AsNoTracking()
                .Where(a => a.BusinessId == businessId && a.CampaignId == campaignId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new CampaignAudienceHistoryItemDto
                {
                    AttachmentId = a.Id,
                    AudienceId = a.AudienceId,
                    AudienceName = a.Audience.Name,
                    CsvBatchId = a.CsvBatchId,
                    FileName = a.FileName,
                    IsActive = a.IsActive,
                    CreatedAt = a.CreatedAt,
                    DeactivatedAt = a.DeactivatedAt,
                    DeactivatedBy = a.DeactivatedBy
                })
                .ToListAsync(ct);
        }

        public async Task<CampaignAudienceReplaceResponseDto> ReplaceAsync(
            Guid businessId,
            Guid campaignId,
            IFormFile csvFile,
            string? audienceName,
            string actor,
            CancellationToken ct = default)
        {
            if (csvFile == null || csvFile.Length <= 0)
                throw new ArgumentException("CSV file is required.", nameof(csvFile));

            // 1) Sent-lock & ownership check first so we do not ingest/allocate data for a locked campaign.
            await EnsureAudienceNotLockedAsync(businessId, campaignId, ct);

            // 2) Ingest into CsvBatch/CsvRows (reusing the existing ingestion pipeline).
            await using var stream = csvFile.OpenReadStream();
            var upload = await _csv.CreateAndIngestAsync(
                businessId: businessId,
                fileName: csvFile.FileName,
                stream: stream,
                audienceId: null,
                campaignId: null, // IMPORTANT: attachment replacement is owned by this service, not by CSV ingest
                ct: ct);

            // 3) Materialize rows from the just-uploaded batch and persist it as the active attachment.
            var req = new CampaignCsvMaterializeRequestDto
            {
                CsvBatchId = upload.BatchId,
                Persist = true,
                AudienceName = string.IsNullOrWhiteSpace(audienceName)
                    ? $"{Path.GetFileNameWithoutExtension(csvFile.FileName)} (CSV)"
                    : audienceName,
                NormalizePhones = true,
                Deduplicate = true,
                Limit = null, // Persist should use ALL rows
                PhoneField = null,
                Mappings = null
            };

            var materialized = await MaterializeBatchAsync(businessId, campaignId, req, ct);
            return await ReplaceFromMaterializationAsync(businessId, campaignId, req, materialized.Preview, actor, ct);
        }

        public async Task<CampaignAudienceRemoveResponseDto> RemoveAsync(Guid businessId, Guid campaignId, string actor, CancellationToken ct = default)
        {
            await EnsureAudienceNotLockedAsync(businessId, campaignId, ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var now = DateTime.UtcNow;

                // Deactivate active attachment (history preserved). No-op if none exists.
                var active = await _db.CampaignAudienceAttachments
                    .Where(a => a.BusinessId == businessId && a.CampaignId == campaignId && a.IsActive)
                    .FirstOrDefaultAsync(ct);

                if (active != null)
                {
                    active.IsActive = false;
                    active.DeactivatedAt = now;
                    active.DeactivatedBy = actor;
                }

                // Delete ONLY CSV-derived recipients; keep manual assignments intact.
                var deleted = await _db.CampaignRecipients
                    .Where(r => r.BusinessId == businessId && r.CampaignId == campaignId && r.AudienceMemberId != null)
                    .ExecuteDeleteAsync(ct);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new CampaignAudienceRemoveResponseDto
                {
                    Active = new CampaignAudienceDto { IsLocked = false },
                    CsvRecipientsDeleted = deleted
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<CampaignAudienceReplaceResponseDto> ReplaceFromMaterializationAsync(
            Guid businessId,
            Guid campaignId,
            CampaignCsvMaterializeRequestDto request,
            IReadOnlyList<CsvMaterializedRowDto> materializedRows,
            string actor,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.CsvBatchId == Guid.Empty) throw new ArgumentException("CsvBatchId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.AudienceName))
                throw new ArgumentException("AudienceName is required when persisting.", nameof(request));

            await EnsureAudienceNotLockedAsync(businessId, campaignId, ct);

            // Persist attachment + recipients atomically.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var now = DateTime.UtcNow;

                // Load batch (for filename) and ensure tenant ownership.
                var batch = await _db.CsvBatches
                    .FirstOrDefaultAsync(b => b.Id == request.CsvBatchId && b.BusinessId == businessId, ct);
                if (batch == null) throw new KeyNotFoundException("CSV batch not found.");

                // 1) Deactivate current active attachment (history row remains) - required for "replace".
                var active = await _db.CampaignAudienceAttachments
                    .Where(a => a.BusinessId == businessId && a.CampaignId == campaignId && a.IsActive)
                    .FirstOrDefaultAsync(ct);

                if (active != null)
                {
                    active.IsActive = false;
                    active.DeactivatedAt = now;
                    active.DeactivatedBy = actor;
                }

                // 2) Create a new Audience that represents the materialized snapshot for this campaign.
                var audience = new Audience
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = request.AudienceName!.Trim(),
                    CsvBatchId = batch.Id,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false
                };
                _db.Audiences.Add(audience);

                // Keep batch -> audience aligned for later export/debug.
                batch.AudienceId = audience.Id;

                // 3) Rebuild recipients (CSV only) and create AudienceMembers for the new Audience.
                // IMPORTANT: Do NOT touch manual recipients (ContactId != null && AudienceMemberId == null).
                var deleted = await _db.CampaignRecipients
                    .Where(r => r.BusinessId == businessId && r.CampaignId == campaignId && r.AudienceMemberId != null)
                    .ExecuteDeleteAsync(ct);

                // Avoid duplicates against manual recipients (best effort by normalized phone).
                var existingManualPhones = await GetExistingManualRecipientPhonesAsync(_db, businessId, campaignId, ct);

                var phones = materializedRows
                    .Select(r => NormalizePhoneDigits(r.Phone))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var contactByPhone = await LoadContactsByPhoneAsync(businessId, phones, ct);

                var membersToAdd = new List<AudienceMember>(capacity: phones.Count);
                var recipientsToAdd = new List<CampaignRecipient>(capacity: phones.Count);

                // Extra safety: always deduplicate inserts by phone, even if request.Deduplicate=false.
                var seenCsvPhones = new HashSet<string>(StringComparer.Ordinal);

                foreach (var r in materializedRows)
                {
                    var phoneDigits = NormalizePhoneDigits(r.Phone);
                    if (string.IsNullOrWhiteSpace(phoneDigits)) continue;

                    if (existingManualPhones.Contains(phoneDigits))
                        continue;

                    if (!seenCsvPhones.Add(phoneDigits))
                        continue;

                    var variables = r.Variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    // Shapes expected by sender:
                    var bodyParams = BuildBodyParamArray(variables);
                    var headerAndButtons = BuildHeaderAndButtonVars(variables);

                    var resolvedParamsJson = JsonSerializer.Serialize(bodyParams);
                    var resolvedButtonsJson = JsonSerializer.Serialize(headerAndButtons);

                    // Deterministic idempotency per (campaign, phone, resolved params).
                    var idemPayload = JsonSerializer.Serialize(new { p = bodyParams, b = headerAndButtons });
                    var idempotencyKey = ComputeIdempotencyKey(campaignId, phoneDigits, idemPayload);

                    contactByPhone.TryGetValue(phoneDigits, out var contactId);

                    var member = new AudienceMember
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        AudienceId = audience.Id,
                        ContactId = contactId,
                        PhoneE164 = phoneDigits,
                        PhoneRaw = null,
                        AttributesJson = JsonSerializer.Serialize(variables),
                        IsTransientContact = !contactId.HasValue,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    membersToAdd.Add(member);

                    var recipient = new CampaignRecipient
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        CampaignId = campaignId,
                        AudienceMemberId = member.Id,
                        ContactId = contactId,
                        IdempotencyKey = idempotencyKey,
                        ResolvedParametersJson = resolvedParamsJson,
                        ResolvedButtonUrlsJson = resolvedButtonsJson,
                        MaterializedAt = now,
                        SentAt = null,
                        Status = "Pending",
                        UpdatedAt = now
                    };

                    recipientsToAdd.Add(recipient);
                }

                if (membersToAdd.Count > 0)
                    await _db.AudienceMembers.AddRangeAsync(membersToAdd, ct);
                if (recipientsToAdd.Count > 0)
                    await _db.CampaignRecipients.AddRangeAsync(recipientsToAdd, ct);

                // 4) Create new active attachment row.
                var attach = new CampaignAudienceAttachment
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    CampaignId = campaignId,
                    AudienceId = audience.Id,
                    CsvBatchId = batch.Id,
                    FileName = batch.FileName,
                    IsActive = true,
                    CreatedAt = now
                };
                _db.CampaignAudienceAttachments.Add(attach);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                Log.Information(
                    "Campaign audience replaced | biz={Biz} campaign={Campaign} batch={Batch} deletedCsvRecipients={Deleted} insertedCsvRecipients={Inserted} actor={Actor}",
                    businessId, campaignId, batch.Id, deleted, recipientsToAdd.Count, actor);

                return new CampaignAudienceReplaceResponseDto
                {
                    Active = new CampaignAudienceDto
                    {
                        AttachmentId = attach.Id,
                        AudienceId = audience.Id,
                        AudienceName = audience.Name,
                        CsvBatchId = batch.Id,
                        FileName = attach.FileName,
                        CreatedAt = attach.CreatedAt,
                        MemberCount = membersToAdd.Count,
                        IsLocked = false
                    },
                    CsvRecipientsInserted = recipientsToAdd.Count
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        private async Task EnsureAudienceNotLockedAsync(Guid businessId, Guid campaignId, CancellationToken ct)
        {
            var (exists, locked) = await GetCampaignExistenceAndLockAsync(businessId, campaignId, ct);
            if (!exists) throw new KeyNotFoundException("Campaign not found.");
            if (locked) throw new AudienceLockedException();
        }

        private async Task<(bool exists, bool locked)> GetCampaignExistenceAndLockAsync(Guid businessId, Guid campaignId, CancellationToken ct)
        {
            if (businessId == Guid.Empty) return (false, false);
            if (campaignId == Guid.Empty) return (false, false);

            var campaign = await _db.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.BusinessId == businessId && !c.IsDeleted)
                .Select(c => new { c.Id, c.Status })
                .FirstOrDefaultAsync(ct);

            if (campaign == null) return (false, false);

            var hasSendLogs = await _db.CampaignSendLogs
                .AsNoTracking()
                .AnyAsync(l => l.BusinessId == businessId && l.CampaignId == campaignId, ct);

            var locked = string.Equals(campaign.Status, "Sent", StringComparison.OrdinalIgnoreCase) || hasSendLogs;
            return (true, locked);
        }

        public sealed class AudienceLockedException : Exception
        {
            public AudienceLockedException() : base("Audience cannot be changed after sending.") { }
        }

        // ---------------- Materialize (shared) ----------------
        // Used by /audience/replace so it can use the same CSV->variables behavior as /materialize.
        private async Task<CampaignCsvMaterializeResponseDto> MaterializeBatchAsync(
            Guid businessId,
            Guid campaignId,
            CampaignCsvMaterializeRequestDto request,
            CancellationToken ct)
        {
            var owns = await _db.Campaigns
                .AsNoTracking()
                .AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct);
            if (!owns) throw new UnauthorizedAccessException("Campaign not found or not owned by this business.");

            var rowsQuery = _db.CsvRows
                .AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.BatchId == request.CsvBatchId)
                .OrderBy(r => r.RowIndex);

            var totalRows = await rowsQuery.CountAsync(ct);
            var csvRows = await rowsQuery.ToListAsync(ct); // Persist path uses ALL rows

            var resp = new CampaignCsvMaterializeResponseDto
            {
                CampaignId = campaignId,
                CsvBatchId = request.CsvBatchId,
                TotalRows = totalRows
            };

            var headerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in csvRows)
                foreach (var k in JsonToDict(r.DataJson).Keys)
                    headerSet.Add(k);

            var phoneField = request.PhoneField;
            if (string.IsNullOrWhiteSpace(phoneField))
                phoneField = PhoneHeaderCandidates.FirstOrDefault(headerSet.Contains);

            if (string.IsNullOrWhiteSpace(phoneField))
                resp.Warnings.Add("No phone field provided or detected; rows without phone will be skipped.");

            var requiredBodySlots = await GetRequiredBodySlotsAsync(businessId, campaignId, ct);
            var namedBodyTokens = await GetNamedBodyTokensAsync(businessId, campaignId, ct);

            var effectiveMappings = request.Mappings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seenPhones = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in csvRows)
            {
                ct.ThrowIfCancellationRequested();

                var rowDict = JsonToDict(row.DataJson);
                var m = new CsvMaterializedRowDto { RowIndex = row.RowIndex };

                var mappingsToUse =
                    (effectiveMappings.Count > 0)
                        ? new Dictionary<string, string>(effectiveMappings, StringComparer.OrdinalIgnoreCase)
                        : BuildAutoMappingsFromRow(rowDict, requiredBodySlots, namedBodyTokens);

                m.Variables = _resolver.ResolveVariables(rowDict, mappingsToUse);

                string? phoneRaw = null;
                if (!string.IsNullOrWhiteSpace(phoneField))
                    rowDict.TryGetValue(phoneField, out phoneRaw);
                else
                    foreach (var cand in PhoneHeaderCandidates)
                        if (rowDict.TryGetValue(cand, out phoneRaw) && !string.IsNullOrWhiteSpace(phoneRaw))
                            break;

                var phoneDigits = NormalizePhoneDigits(phoneRaw, request.NormalizePhones);
                m.Phone = phoneDigits;

                if (string.IsNullOrWhiteSpace(m.Phone))
                {
                    m.Errors.Add("Missing phone");
                    resp.SkippedCount++;
                    continue;
                }

                if (request.Deduplicate && !seenPhones.Add(m.Phone))
                {
                    m.Errors.Add("Duplicate phone (deduped)");
                    resp.SkippedCount++;
                    continue;
                }

                var prelimBodyParams = BuildBodyParamArrayFromVariables(m.Variables);
                var enforced = EnsureBodyParamsComplete(prelimBodyParams, requiredBodySlots, out var missingSlots);
                if (enforced == null)
                {
                    m.Errors.Add($"Missing body parameters: {string.Join(", ", missingSlots)}");
                    resp.SkippedCount++;
                    continue;
                }

                resp.Preview.Add(m);
            }

            resp.MaterializedCount = resp.Preview.Count;
            return resp;
        }

        private static Dictionary<string, string> JsonToDict(string? json)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return dict;
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? "" : p.Value.ToString();

            return dict;
        }

        // Canonical "digits-only" E.164-ish normalization (no leading '+'), aligned with other backend validators.
        private static string? NormalizePhoneDigits(string? raw, bool normalize = true)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!normalize) return raw.Trim();
            return PhoneNumberNormalizer.NormalizeToE164Digits(raw, "IN");
        }

        private static string NormalizePhoneDigits(string? raw)
            => NormalizePhoneDigits(raw, normalize: true) ?? string.Empty;

        private static string ComputeIdempotencyKey(Guid campaignId, string phoneDigits, string parametersJson)
        {
            var raw = $"{campaignId}|{phoneDigits}|{parametersJson}";
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }

        private static string[] BuildBodyParamArrayFromVariables(IDictionary<string, string> vars)
        {
            var pairs = new List<(int idx, string val)>();
            foreach (var kv in vars)
            {
                var k = kv.Key;
                var v = kv.Value ?? string.Empty;

                if (k.StartsWith("body.", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(k.AsSpan("body.".Length), out var n1) && n1 > 0)
                {
                    pairs.Add((n1, v));
                    continue;
                }

                if (k.StartsWith("parameter", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(k.AsSpan("parameter".Length), out var n2) && n2 > 0)
                {
                    pairs.Add((n2, v));
                }

                var m = Regex.Match(k, @"^\{\{\s*(\d+)\s*\}\}$");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var t) && t > 0)
                {
                    pairs.Add((t, v));
                }
            }

            if (pairs.Count == 0) return Array.Empty<string>();

            var max = pairs.Max(p => p.idx);
            var arr = new string[max];
            for (int i = 0; i < max; i++) arr[i] = string.Empty;
            foreach (var (idx, val) in pairs) arr[idx - 1] = val ?? string.Empty;
            return arr;
        }

        private static string[]? EnsureBodyParamsComplete(string[] bodyParams, int requiredSlots, out List<string> missing)
        {
            missing = new List<string>();
            if (requiredSlots <= 0) return bodyParams;

            var arr = new string[requiredSlots];
            for (int i = 0; i < requiredSlots; i++)
            {
                var v = (i < bodyParams.Length ? bodyParams[i] : string.Empty) ?? string.Empty;
                arr[i] = v;
                if (string.IsNullOrWhiteSpace(v))
                    missing.Add($"{{{{{i + 1}}}}}");
            }

            return missing.Count > 0 ? null : arr;
        }

        private static string[] BuildBodyParamArray(IDictionary<string, string> vars)
        {
            var pairs = new List<(int idx, string val)>();

            foreach (var kv in vars)
            {
                var k = kv.Key;

                if (k.StartsWith("body.", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(k.AsSpan("body.".Length), out var n) && n > 0)
                        pairs.Add((n, kv.Value ?? string.Empty));
                    continue;
                }

                if (k.StartsWith("parameter", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(k.AsSpan("parameter".Length), out var n) && n > 0)
                        pairs.Add((n, kv.Value ?? string.Empty));
                }
            }

            if (pairs.Count == 0) return Array.Empty<string>();

            var max = pairs.Max(p => p.idx);
            var arr = new string[max];
            for (int i = 0; i < max; i++) arr[i] = string.Empty;
            foreach (var (idx, val) in pairs) arr[idx - 1] = val ?? string.Empty;
            return arr;
        }

        private static Dictionary<string, string> BuildHeaderAndButtonVars(IDictionary<string, string> vars)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in vars)
            {
                var k = kv.Key;
                var v = kv.Value ?? string.Empty;

                if (k.StartsWith("header.", StringComparison.OrdinalIgnoreCase) &&
                    (k.EndsWith("_url", StringComparison.OrdinalIgnoreCase) ||
                     k.EndsWith(".url", StringComparison.OrdinalIgnoreCase)))
                {
                    dict[k] = v;
                    continue;
                }

                if (k.StartsWith("header.text.", StringComparison.OrdinalIgnoreCase))
                {
                    var tail = k.Substring("header.text.".Length);
                    if (int.TryParse(tail, out var n) && n > 0)
                        dict[k] = v;
                    continue;
                }

                if (k.StartsWith("button", StringComparison.OrdinalIgnoreCase))
                {
                    var normKey = k
                        .Replace(".url.param", ".url_param", StringComparison.OrdinalIgnoreCase)
                        .Replace(".urlparam", ".url_param", StringComparison.OrdinalIgnoreCase);

                    if (normKey.EndsWith(".url_param", StringComparison.OrdinalIgnoreCase))
                        dict[normKey] = v;
                }
            }

            return dict;
        }

        private static Dictionary<string, string> BuildAutoMappingsFromRow(
            IDictionary<string, string> rowDict,
            int requiredBodySlots,
            IReadOnlyCollection<string>? namedBodyTokens = null)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int n = requiredBodySlots > 0
                ? requiredBodySlots
                : rowDict.Keys.Select(k =>
                {
                    var m = Regex.Match(k, @"^parameter(\d+)$", RegexOptions.IgnoreCase);
                    return m.Success ? int.Parse(m.Groups[1].Value) : 0;
                }).DefaultIfEmpty(0).Max();

            for (int i = 1; i <= n; i++)
            {
                var csvHeader = rowDict.Keys.FirstOrDefault(k => string.Equals(k, $"parameter{i}", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(csvHeader))
                    map[$"{{{{{i}}}}}"] = csvHeader;
            }

            if (namedBodyTokens != null && namedBodyTokens.Count > 0)
            {
                foreach (var token in namedBodyTokens)
                {
                    var csvHeader = rowDict.Keys.FirstOrDefault(k => string.Equals(k, token, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(csvHeader))
                        map[$"{{{{{token}}}}}"] = csvHeader;
                }
            }

            foreach (var kv in rowDict)
            {
                var m = Regex.Match(kv.Key, @"^headerpara(\d+)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var slot = int.Parse(m.Groups[1].Value);
                    map[$"header.text_param{slot}"] = kv.Key;
                }
            }

            foreach (var kv in rowDict)
            {
                var m = Regex.Match(kv.Key, @"^buttonpara(\d+)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var pos = int.Parse(m.Groups[1].Value);
                    if (pos >= 1 && pos <= 3)
                        map[$"button{pos}.url_param"] = kv.Key;
                }
            }

            return map;
        }

        private async Task<int> GetRequiredBodySlotsAsync(Guid businessId, Guid campaignId, CancellationToken ct)
        {
            var snap = await _db.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.BusinessId == businessId)
                .Select(c => new { c.TemplateId, c.MessageTemplate })
                .FirstOrDefaultAsync(ct);

            if (snap is null) return 0;

            WhatsAppTemplate? tpl = null;
            if (!string.IsNullOrWhiteSpace(snap.TemplateId))
            {
                tpl = await _db.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(t => t.BusinessId == businessId && t.IsActive && t.Name == snap.TemplateId)
                    .OrderByDescending(t => t.UpdatedAt)
                    .ThenByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync(ct);
            }

            if (tpl == null && !string.IsNullOrWhiteSpace(snap.MessageTemplate))
            {
                tpl = await _db.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(t => t.BusinessId == businessId && t.IsActive && t.Name == snap.MessageTemplate)
                    .OrderByDescending(t => t.UpdatedAt)
                    .ThenByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync(ct);
            }

            return tpl?.BodyVarCount ?? 0;
        }

        private async Task<IReadOnlyList<string>> GetNamedBodyTokensAsync(Guid businessId, Guid campaignId, CancellationToken ct)
        {
            var data = await _db.Campaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && c.BusinessId == businessId)
                .Select(c => new { c.MessageTemplate, c.TemplateId })
                .FirstOrDefaultAsync(ct);

            var templateName = !string.IsNullOrWhiteSpace(data?.TemplateId)
                ? data!.TemplateId!
                : (data?.MessageTemplate ?? string.Empty);

            if (string.IsNullOrWhiteSpace(templateName))
                return Array.Empty<string>();

            var tpl = await _db.WhatsAppTemplates
                .AsNoTracking()
                .Where(t => t.BusinessId == businessId && t.IsActive && t.Name == templateName)
                .OrderByDescending(t => t.UpdatedAt > t.CreatedAt ? t.UpdatedAt : t.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (tpl == null) return Array.Empty<string>();

            var summary = xbytechat.api.WhatsAppSettings.Helpers.TemplateJsonHelper
                .SummarizeDetailed(tpl.RawJson, tpl.Body);

            return summary.Placeholders
                .Where(p => p.Location == xbytechat.api.WhatsAppSettings.Helpers.PlaceholderLocation.Body
                         && p.Type == xbytechat.api.WhatsAppSettings.Helpers.PlaceholderType.Named)
                .Select(p => p.Name!)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static async Task<HashSet<string>> GetExistingManualRecipientPhonesAsync(
            AppDbContext db,
            Guid businessId,
            Guid campaignId,
            CancellationToken ct)
        {
            // Manual recipients are assigned by Contact (AudienceMemberId == null). We keep them untouched,
            // and also avoid inserting a CSV-derived duplicate for the same normalized phone.
            var contactPhones = await db.CampaignRecipients
                .AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.CampaignId == campaignId && r.AudienceMemberId == null && r.ContactId != null)
                .Join(db.Contacts.AsNoTracking(),
                    r => r.ContactId!.Value,
                    c => c.Id,
                    (r, c) => c.PhoneNumber)
                .ToListAsync(ct);

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in contactPhones)
            {
                var norm = PhoneNumberNormalizer.NormalizeToE164Digits(p, "IN");
                if (!string.IsNullOrWhiteSpace(norm)) set.Add(norm);
            }

            return set;
        }

        private async Task<Dictionary<string, Guid?>> LoadContactsByPhoneAsync(Guid businessId, IReadOnlyList<string> phoneDigits, CancellationToken ct)
        {
            var result = new Dictionary<string, Guid?>(StringComparer.Ordinal);
            if (phoneDigits.Count == 0) return result;

            var candidates = phoneDigits
                .SelectMany(p => new[] { p, "+" + p })
                .Distinct()
                .ToList();

            var contacts = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.BusinessId == businessId && candidates.Contains(c.PhoneNumber))
                .Select(c => new { c.Id, c.PhoneNumber })
                .ToListAsync(ct);

            foreach (var c in contacts)
            {
                var norm = PhoneNumberNormalizer.NormalizeToE164Digits(c.PhoneNumber, "IN");
                if (string.IsNullOrWhiteSpace(norm)) continue;
                result[norm] = c.Id;
            }

            return result;
        }
    }
}
