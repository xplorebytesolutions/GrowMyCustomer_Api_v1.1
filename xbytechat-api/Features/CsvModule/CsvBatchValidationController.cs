using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Shared;
using xbytechat_api.WhatsAppSettings.Services; // User.GetBusinessId()

namespace xbytechat.api.Features.CsvModule.Controllers
{
    [ApiController]
    [Route("api/csv/batch/{batchId:guid}/validate")]
    [Authorize]
    public sealed class CsvBatchValidationController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CsvBatchValidationController(AppDbContext db) => _db = db;

        public sealed class ValidateRequest
        {
            public string? PhoneHeader { get; set; }              // e.g. "phone"
            public List<string>? RequiredHeaders { get; set; }    // e.g. ["parameter1","headerpara1","buttonpara1"]
            public bool NormalizePhone { get; set; } = true;
            public bool CheckDuplicates { get; set; } = true;
            public int? Limit { get; set; }                       // optional sample cap
        }

        public sealed class ValidateResponse
        {
            public bool Success { get; set; } = true;
            public List<string> Problems { get; set; } = new();
            public object Stats { get; set; } = new { rows = 0, missingPhone = 0, invalidPhones = 0, duplicatePhones = 0 };
            public List<string> Headers { get; set; } = new();    // discovered headers in the batch
        }

        [HttpPost]
        public async Task<IActionResult> Validate(Guid batchId, [FromBody] ValidateRequest req, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            // Load CSV rows for this batch (owned by business)
            var rowsQ = _db.CsvRows
                .AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.BatchId == batchId)
                .OrderBy(r => r.RowIndex);

            var total = await rowsQ.CountAsync(ct);
            if (total == 0)
                return Ok(new ValidateResponse
                {
                    Problems = new List<string> { "CSV batch is empty." },
                    Stats = new { rows = 0, missingPhone = 0, invalidPhones = 0, duplicatePhones = 0 },
                    Headers = new List<string>()
                });

            var rows = req.Limit.HasValue && req.Limit.Value > 0
                ? await rowsQ.Take(req.Limit.Value).ToListAsync(ct)
                : await rowsQ.ToListAsync(ct);

            // Discover header set by union of row keys (case-insensitive compare)
            var headerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
                foreach (var k in KeysOfJson(r.DataJson))
                    headerSet.Add(k);

            var headers = headerSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            var problems = new List<string>();

            // Validate phone header presence
            var phoneHeader = (req.PhoneHeader ?? "").Trim();
            if (string.IsNullOrWhiteSpace(phoneHeader))
            {
                // Try helpful guesses
                var guesses = new[] { "phone", "mobile", "whatsapp", "number", "phonee164", "msisdn", "whatsapp_number" };
                var guess = guesses.FirstOrDefault(h => headerSet.Contains(h));
                if (!string.IsNullOrEmpty(guess))
                    phoneHeader = guess;
            }

            if (string.IsNullOrWhiteSpace(phoneHeader))
            {
                problems.Add("Phone column not specified and could not be guessed.");
            }
            else if (!headerSet.Contains(phoneHeader))
            {
                problems.Add($"Phone column “{phoneHeader}” not found in CSV.");
            }

            // Validate requiredHeaders presence (parameterN/headerparaN/buttonparaN)
            var required = req.RequiredHeaders ?? new List<string>();
            foreach (var h in required)
            {
                if (!headerSet.Contains(h))
                    problems.Add($"Required column “{h}” is missing.");
            }

            // Row-level checks
            int missingPhone = 0, invalidPhones = 0, duplicatePhones = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var r in rows)
            {
                var dict = JsonToDict(r.DataJson);

                // phone
                string? rawPhone = null;
                if (!string.IsNullOrWhiteSpace(phoneHeader))
                    dict.TryGetValue(phoneHeader, out rawPhone);

                var normPhone = NormalizePhoneMaybe(rawPhone, req.NormalizePhone);
                if (string.IsNullOrWhiteSpace(normPhone))
                {
                    missingPhone++;
                    continue;
                }

                // naive validity check
                if (!Regex.IsMatch(normPhone, @"^\d{10,15}$"))
                {
                    invalidPhones++;
                }

                if (req.CheckDuplicates && !seen.Add(normPhone))
                {
                    duplicatePhones++;
                }
            }

            var resp = new ValidateResponse
            {
                Problems = problems,
                Stats = new { rows = total, missingPhone, invalidPhones, duplicatePhones },
                Headers = headers
            };

            return Ok(resp);
        }

        // ---------- helpers ----------
        private static IEnumerable<string> KeysOfJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) yield break;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) yield break;
            foreach (var p in doc.RootElement.EnumerateObject())
                yield return p.Name;
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

        private static string? NormalizePhoneMaybe(string? raw, bool normalize)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim();
            if (!normalize) return trimmed;

            // simple E.164-ish cleanup
            var digits = Regex.Replace(trimmed, "[^0-9]", "");
            digits = digits.TrimStart('0');
            if (digits.Length == 10) digits = "91" + digits; // heuristic India
            return digits;
        }
    }
}
