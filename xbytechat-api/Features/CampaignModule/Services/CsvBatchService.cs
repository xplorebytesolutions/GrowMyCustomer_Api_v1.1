using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.CampaignModule.CountryCodes;
namespace xbytechat.api.Features.CampaignModule.Services
{
    public class CsvBatchService : ICsvBatchService
    {
        private readonly AppDbContext _db;

        public CsvBatchService(AppDbContext db)
        {
            _db = db;

        }

        // ----------------------------
        // Upload + ingest
        // ----------------------------
        public async Task<CsvBatchUploadResultDto> CreateAndIngestAsync(
        Guid businessId,
        string fileName,
        Stream stream,
        Guid? audienceId = null,
        Guid? campaignId = null,
        CancellationToken ct = default)
        {
            // If we’re in a campaign context and no audience was supplied, create one now.
            Audience? audience = null;
            if (audienceId is null && campaignId is not null)
            {
                var campaignExists = await _db.Campaigns
                    .AnyAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct);
                if (!campaignExists)
                    throw new InvalidOperationException("Campaign not found for this business.");

                audience = new Audience
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    CampaignId = campaignId,                  // ← link to campaign (so delete cascades)
                    Name = Path.GetFileNameWithoutExtension(fileName) + " (CSV)",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _db.Audiences.Add(audience);
                await _db.SaveChangesAsync(ct);              // get Audience.Id
                audienceId = audience.Id;                    // feed it into the batch
            }

            // 1) Create batch shell (now with a guaranteed AudienceId if campaignId was provided)
            var batch = new CsvBatch
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                AudienceId = audienceId,                     // ← no longer null for campaign CSVs
                FileName = fileName,
                CreatedAt = DateTime.UtcNow,
                Status = "ingesting",
                RowCount = 0,
                SkippedCount = 0,
                HeadersJson = null
            };

            _db.CsvBatches.Add(batch);
            await _db.SaveChangesAsync(ct);

            // Keep both sides in sync if we created the audience here.
            if (audience is not null)
            {
                audience.CsvBatchId = batch.Id;              // ← your model maps this too
                await _db.SaveChangesAsync(ct);
            }

            try
            {
                // ───── your existing parsing logic (unchanged) ─────
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

                string? headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    var headers = new List<string> { "phone" };
                    batch.HeadersJson = JsonSerializer.Serialize(headers);
                    batch.Status = "ready";
                    await _db.SaveChangesAsync(ct);

                    Log.Warning("CSV had no header line. Created batch {BatchId} with fallback 'phone' header.", batch.Id);

                    return new CsvBatchUploadResultDto
                    {
                        BatchId = batch.Id,
                        AudienceId = batch.AudienceId,
                        FileName = batch.FileName ?? string.Empty,
                        RowCount = 0,
                        Headers = headers
                    };
                }

                var delim = DetectDelimiter(headerLine);
                var headersParsed = ParseCsvLine(headerLine, delim)
                    .Select(h => h.Trim())
                    .Where(h => !string.IsNullOrEmpty(h))
                    .ToList();

                if (headersParsed.Count == 0)
                    headersParsed = new List<string> { "phone" };

                //batch.HeadersJson = JsonSerializer.Serialize(headersParsed);
                //await _db.SaveChangesAsync(ct);

                //var rowsBuffer = new List<CsvRow>(capacity: 1024);
                //int rowIndex = 0;
                batch.HeadersJson = JsonSerializer.Serialize(headersParsed);
                await _db.SaveChangesAsync(ct);

                var rowsBuffer = new List<CsvRow>(capacity: 1024);
                int rowIndex = 0;

                // Detect the phone column once from the header row.
                // We try common names; falls back to exact "phone" if present.
                string? phoneHeader =
                    headersParsed.FirstOrDefault(h =>
                        PhoneHeaderCandidates.Any(c => c.Equals(h, StringComparison.OrdinalIgnoreCase)))
                    ?? headersParsed.FirstOrDefault(h => h.Equals("phone", StringComparison.OrdinalIgnoreCase));

                if (phoneHeader == null)
                {
                    Log.Warning("CsvBatch {BatchId}: no phone-like header found. Headers={Headers}",
                        batch.Id, string.Join(", ", headersParsed));
                }

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = ParseCsvLine(line, delim);

                    var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < headersParsed.Count; i++)
                    {
                        var key = headersParsed[i];
                        var val = i < cols.Count ? cols[i]?.Trim() : null;
                        dict[key] = val;
                    }

                    // Pull phone value (if we detected a phone header)
                    string? phoneRaw = null;
                    if (!string.IsNullOrEmpty(phoneHeader))
                    {
                        dict.TryGetValue(phoneHeader, out phoneRaw);
                    }

                    // Normalize using your existing validator (E.164); capture error if any
                    string? phoneErr;
                    var phoneE164 = NormalizeToE164OrError(phoneRaw, out phoneErr);

                    // Buffer the row with both raw + normalized phone and the whole row JSON
                    rowsBuffer.Add(new CsvRow
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        BatchId = batch.Id,
                        RowIndex = rowIndex++,

                        PhoneRaw = phoneRaw,
                        PhoneE164 = phoneE164,
                        ValidationError = (phoneE164 is null && !string.IsNullOrWhiteSpace(phoneRaw)) ? phoneErr : null,

                        // Persist the entire row as JSON (your model maps DataJson -> RowJson)
                        RowJson = JsonSerializer.Serialize(dict)
                    });


                    if (rowsBuffer.Count >= 1000)
                    {
                        _db.CsvRows.AddRange(rowsBuffer);
                        await _db.SaveChangesAsync(ct);
                        rowsBuffer.Clear();
                    }
                }

                if (rowsBuffer.Count > 0)
                {
                    _db.CsvRows.AddRange(rowsBuffer);
                    await _db.SaveChangesAsync(ct);
                }

                batch.RowCount = rowIndex;
                batch.Status = "ready";
                await _db.SaveChangesAsync(ct);

                Log.Information("CsvBatch {BatchId} ingested: {Rows} rows; headers={HeaderCount}", batch.Id, batch.RowCount, headersParsed.Count);

                return new CsvBatchUploadResultDto
                {
                    BatchId = batch.Id,
                    AudienceId = batch.AudienceId,           // now reliably set for campaign CSVs
                    FileName = batch.FileName ?? string.Empty,
                    RowCount = batch.RowCount,
                    Headers = headersParsed
                };
            }
            catch (Exception ex)
            {
                batch.Status = "failed";
                batch.ErrorMessage = ex.Message;
                await _db.SaveChangesAsync(ct);
                Log.Error(ex, "CSV ingest failed for batch {BatchId}", batch.Id);
                throw;
            }
        }

        // ----------------------------
        // Batch info
        // ----------------------------
        private async Task<CsvBatchUploadResultDto> IngestCoreAsync(
            Guid businessId,
            string fileName,
            Stream stream,
            CancellationToken ct)
        {
            // Minimal “stage only” helper (kept in case other code calls it)
            var batch = new CsvBatch
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                FileName = fileName,
                CreatedAt = DateTime.UtcNow,
                Status = "ready",
                HeadersJson = null,
                RowCount = 0,
                SkippedCount = 0,
                ErrorMessage = null
            };
            _db.CsvBatches.Add(batch);
            await _db.SaveChangesAsync(ct);

            Log.Information("CsvBatch {BatchId} staged for business {Biz}", batch.Id, businessId);

            return new CsvBatchUploadResultDto
            {
                BatchId = batch.Id,
                AudienceId = null,
                FileName = fileName,
                RowCount = 0,
                Headers = new List<string>(),
                Message = "CSV batch created."
            };
        }

        public async Task<CsvBatchInfoDto?> GetBatchAsync(Guid businessId, Guid batchId, CancellationToken ct = default)
        {
            var batch = await _db.CsvBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId && b.BusinessId == businessId, ct);

            if (batch == null) return null;

            var headers = SafeParseHeaderArray(batch.HeadersJson);

            return new CsvBatchInfoDto
            {
                BatchId = batch.Id,
                AudienceId = batch.AudienceId,
                RowCount = batch.RowCount,
                Headers = headers,
                CreatedAt = batch.CreatedAt
            };
        }

        // ----------------------------
        // Samples (single implementation)
        // ----------------------------
        public async Task<IReadOnlyList<CsvRowSampleDto>> GetSamplesAsync(
            Guid businessId,
            Guid batchId,
            int take = 20,
            CancellationToken ct = default)
        {
            if (take <= 0) take = 20;
            if (take > 100) take = 100;

            var batch = await _db.CsvBatches
                .AsNoTracking()
                .Where(b => b.Id == batchId && b.BusinessId == businessId)
                .Select(b => new { b.Id, b.HeadersJson, b.RowCount })
                .FirstOrDefaultAsync(ct);

            if (batch is null)
                throw new KeyNotFoundException("Batch not found.");

            // If no rows yet, return empty samples gracefully
            if (batch.RowCount <= 0)
                return Array.Empty<CsvRowSampleDto>();

            var headerList = SafeParseHeaderArray(batch.HeadersJson);

            var rows = await _db.CsvRows
                .AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.BatchId == batchId)
                .OrderBy(r => r.RowIndex)
                .Take(take)
                .Select(r => new { r.RowIndex, r.DataJson })
                .ToListAsync(ct);

            var result = new List<CsvRowSampleDto>(rows.Count);
            foreach (var r in rows)
            {
                var dict = SafeParseDict(r.DataJson);

                // Ensure consistent header order (fill missing with null)
                var ordered = new Dictionary<string, string?>(headerList.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var h in headerList)
                {
                    dict.TryGetValue(h, out var v);
                    ordered[h] = v;
                }

                result.Add(new CsvRowSampleDto
                {
                    RowIndex = r.RowIndex,
                    Data = ordered
                });
            }

            return result;
        }

        // ----------------------------
        // List / Page / Delete / Validate
        // ----------------------------
        public async Task<List<CsvBatchListItemDto>> ListBatchesAsync(Guid businessId, int limit = 20, CancellationToken ct = default)
        {
            if (limit <= 0) limit = 20;
            if (limit > 100) limit = 100;

            return await _db.CsvBatches
                .AsNoTracking()
                .Where(b => b.BusinessId == businessId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(limit)
                .Select(b => new CsvBatchListItemDto
                {
                    BatchId = b.Id,
                    FileName = b.FileName,
                    RowCount = b.RowCount,
                    Status = b.Status,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync(ct);
        }

        public async Task<CsvBatchRowsPageDto> GetRowsPageAsync(Guid businessId, Guid batchId, int skip, int take, CancellationToken ct = default)
        {
            if (take <= 0) take = 50;
            if (take > 200) take = 200;
            if (skip < 0) skip = 0;

            var exists = await _db.CsvBatches.AsNoTracking()
                .AnyAsync(b => b.Id == batchId && b.BusinessId == businessId, ct);
            if (!exists) throw new KeyNotFoundException("CSV batch not found.");

            var total = await _db.CsvRows.AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.BatchId == batchId)
                .CountAsync(ct);

            var rows = await _db.CsvRows.AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.BatchId == batchId)
                .OrderBy(r => r.RowIndex)
                .Skip(skip)
                .Take(take)
                .Select(r => new CsvRowSampleDto
                {
                    RowIndex = r.RowIndex,
                    Data = SafeParseDict(r.DataJson)
                })
                .ToListAsync(ct);

            return new CsvBatchRowsPageDto
            {
                BatchId = batchId,
                TotalRows = total,
                Skip = skip,
                Take = take,
                Rows = rows
            };
        }

        public async Task<bool> DeleteBatchAsync(Guid businessId, Guid batchId, CancellationToken ct = default)
        {
            var batch = await _db.CsvBatches
                .FirstOrDefaultAsync(b => b.Id == batchId && b.BusinessId == businessId, ct);

            if (batch == null) return false;

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var rows = _db.CsvRows.Where(r => r.BusinessId == businessId && r.BatchId == batchId);
                _db.CsvRows.RemoveRange(rows);

                _db.CsvBatches.Remove(batch);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        private static readonly string[] PhoneHeaderCandidates =
        {  "phone", "phone_number", "phonenumber", "phoneNumber", "phone-no", "phone_no",
             "mobile", "mobile_number", "mobilenumber", "whatsapp", "whatsapp_no", "whatsapp_number" };

        public async Task<CsvBatchValidationResultDto> ValidateAsync(
            Guid businessId,
            Guid batchId,
            CsvBatchValidationRequestDto request,
            CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (request.SampleSize <= 0) request.SampleSize = 20;
            if (request.SampleSize > 100) request.SampleSize = 100;

            var batch = await _db.CsvBatches.AsNoTracking()
                .Where(b => b.BusinessId == businessId && b.Id == batchId)
                .Select(b => new { b.Id, b.HeadersJson, b.RowCount })
                .FirstOrDefaultAsync(ct);

            if (batch == null) throw new KeyNotFoundException("CSV batch not found.");

            var headers = SafeParseHeaderArray(batch.HeadersJson);
            var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);

            var result = new CsvBatchValidationResultDto
            {
                BatchId = batchId,
                TotalRows = batch.RowCount
            };

            // Required headers check
            if (request.RequiredHeaders != null && request.RequiredHeaders.Count > 0)
            {
                foreach (var req in request.RequiredHeaders)
                {
                    if (!headerSet.Contains(req))
                        result.MissingRequiredHeaders.Add(req);
                }

                if (result.MissingRequiredHeaders.Count > 0)
                    result.Errors.Add("Required headers are missing.");
            }

            // Determine phone field
            var phoneField = request.PhoneField;
            if (string.IsNullOrWhiteSpace(phoneField))
                phoneField = PhoneHeaderCandidates.FirstOrDefault(headerSet.Contains);

            result.PhoneField = phoneField;

            if (string.IsNullOrWhiteSpace(phoneField))
            {
                result.Errors.Add("No phone field provided or detected.");
                return result; // cannot scan rows without a phone column
            }

            // Scan rows for phone presence & duplicates
            var seenPhones = new HashSet<string>(StringComparer.Ordinal);
            var problemSamples = new List<CsvRowSampleDto>();

            var rowsQuery = _db.CsvRows.AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.BatchId == batchId)
                .OrderBy(r => r.RowIndex)
                .Select(r => new { r.RowIndex, r.DataJson });

            await foreach (var row in rowsQuery.AsAsyncEnumerable().WithCancellation(ct))
            {
                var dict = SafeParseDict(row.DataJson);
                dict.TryGetValue(phoneField, out var rawPhone);

                // var normalized = NormalizePhoneMaybe(rawPhone, request.NormalizePhones);
                var normalized = NormalizeToE164OrError(rawPhone, out var phoneErr);
                var isProblem = false;

                if (string.IsNullOrWhiteSpace(normalized))
                {
                    // failed normalization → treat as missing/invalid phone
                    result.MissingPhoneCount++;
                    isProblem = true;
                    if (problemSamples.Count < request.SampleSize)
                    {
                        // include the error text in the sample row so UI can show why it failed
                        var dictWithErr = SafeParseDict(row.DataJson);
                        dictWithErr["__phone_error__"] = phoneErr;
                        problemSamples.Add(new CsvRowSampleDto { RowIndex = row.RowIndex, Data = dictWithErr });
                    }
                }
                else if (request.Deduplicate && !seenPhones.Add(normalized))
                {
                    result.DuplicatePhoneCount++;
                    isProblem = true;
                }

                if (isProblem && problemSamples.Count < request.SampleSize)
                {
                    problemSamples.Add(new CsvRowSampleDto
                    {
                        RowIndex = row.RowIndex,
                        Data = dict
                    });
                }
            }

            result.ProblemSamples = problemSamples;

            if (result.MissingPhoneCount > 0)
                result.Errors.Add("Some rows are missing phone numbers.");
            if (result.DuplicatePhoneCount > 0)
                result.Warnings.Add("Duplicate phone numbers detected (after normalization).");

            return result;
        }

        // ----------------------------
        // helpers
        // ----------------------------
        private static List<string> SafeParseHeaderArray(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new List<string>()
                    : (JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>());
            }
            catch { return new List<string>(); }
        }

        private static Dictionary<string, string?> SafeParseDict(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new Dictionary<string, string?>()
                    : (JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ??
                       new Dictionary<string, string?>());
            }
            catch { return new Dictionary<string, string?>(); }
        }

        private static char DetectDelimiter(string headerLine)
        {
            var candidates = new[] { ',', ';', '\t' };
            var counts = candidates.Select(c => (c, count: headerLine.Count(ch => ch == c))).ToList();
            var best = counts.OrderByDescending(x => x.count).First();
            return best.count > 0 ? best.c : ',';
        }

        /// <summary>
        /// CSV parser with delimiter support: handles commas/semicolons/tabs, double quotes,
        /// and escaped quotes (""). It does NOT support embedded newlines inside quoted fields.
        /// </summary>
        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            if (line == null) return result;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Handle escaped quote ""
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == delimiter)
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }


        private static readonly HashSet<string> VALID_CC = new(StringComparer.Ordinal)
        {
            // 1-digit
            "1",          // NANP (US/CA)
            // 2-digit - common targets
            "20","27","30","31","32","33","34","36","39","40","41","43","44","45","46","47","48","49",
            "51","52","53","54","55","56","57","58","60","61","62","63","64","65","66",
            "81","82","84","86","90","91","92","93","94","95","98",
            // 3-digit - GCC & a few others you likely need
            "966","971"   // SA, AE
        };

        private static string? NormalizeToE164OrError(string? input, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(input)) { error = "Empty phone."; return null; }

            var s = input.Trim();

            // Case A: already +E.164
            if (s.StartsWith("+", StringComparison.Ordinal))
            {
                if (!IsE164(s)) { error = "Must be +E.164: '+' followed by 8–15 digits."; return null; }

                var noPlus = s.Substring(1);
                var cc = ExtractCcByLongestPrefix(noPlus);
                if (string.IsNullOrEmpty(cc)) { error = "Unsupported country code."; return null; }

                // basic NN sanity for a few big markets; keep permissive otherwise
                var nn = noPlus.Substring(cc.Length);
                if (!PassesBasicNationalLength(cc, nn)) { error = $"Invalid national length for +{cc}."; return null; }

                return s;
            }

            // Case B: digits only
            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length < 11 || digits.Length > 15)
            { error = "Include country code (11–15 digits if no '+')."; return null; }

            var cc2 = ExtractCcByLongestPrefix(digits);
            if (string.IsNullOrEmpty(cc2))
            { error = "Unsupported country code."; return null; }

            var nn2 = digits.Substring(cc2.Length);
            if (!PassesBasicNationalLength(cc2, nn2))
            { error = $"Invalid national length for +{cc2}."; return null; }

            return "+" + digits;
        }

        private static bool IsE164(string s)
        {
            if (s.Length < 9 || s.Length > 16) return false; // '+' + 8..15 digits
            for (int i = 1; i < s.Length; i++) if (!char.IsDigit(s[i])) return false;
            return true;
        }

        private static string ExtractCcByLongestPrefix(string digitsNoPlus)
        {
            if (digitsNoPlus.Length >= 3 && VALID_CC.Contains(digitsNoPlus[..3])) return digitsNoPlus[..3];
            if (digitsNoPlus.Length >= 2 && VALID_CC.Contains(digitsNoPlus[..2])) return digitsNoPlus[..2];
            if (digitsNoPlus.Length >= 1 && VALID_CC.Contains(digitsNoPlus[..1])) return digitsNoPlus[..1];
            return "";
        }


        // Pragmatic checks for common markets; keep permissive for others (4..12)
        private static bool PassesBasicNationalLength(string cc, string nn)
        {
            if (cc == "1") return nn.Length == 10; // NANP
            if (cc == "91") return nn.Length == 10; // India
            if (cc == "94") return nn.Length == 9;  // Sri Lanka
            if (cc == "966") return nn.Length == 9;  // Saudi Arabia
            return nn.Length >= 4 && nn.Length <= 12;
        }

    }
}

