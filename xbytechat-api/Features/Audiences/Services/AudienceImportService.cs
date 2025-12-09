using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.Audiences.DTOs;

namespace xbytechat.api.Features.Audiences.Services
{
    public class AudienceImportService : IAudienceImportService
    {
        private readonly AppDbContext _db;

        public AudienceImportService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<CsvImportResponseDto> ImportCsvAsync(
            Guid businessId,
            Stream csvStream,
            string fileName,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new UnauthorizedAccessException("Invalid business id.");

            if (csvStream == null || !csvStream.CanRead)
                throw new ArgumentException("CSV stream is not readable.");

            using var reader = new StreamReader(csvStream);

            // --- header row ---
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
                throw new InvalidOperationException("Empty CSV.");

            var headers = headerLine.Split(',')
                                    .Select(h => (h ?? string.Empty).Trim())
                                    .Where(h => !string.IsNullOrWhiteSpace(h))
                                    .ToList();

            if (headers.Count == 0)
                throw new InvalidOperationException("No columns.");

            var batchId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Pre-create batch
            _db.CsvBatches.Add(new Features.CampaignModule.Models.CsvBatch
            {
                Id = batchId,
                BusinessId = businessId,
                FileName = fileName,
                // ✅ match your model: CsvBatch.HeadersJson
                HeadersJson = Newtonsoft.Json.JsonConvert.SerializeObject(headers),
                RowCount = 0,
                CreatedAt = now
            });

            var rowsBuffer = new List<Features.CampaignModule.Models.CsvRow>(capacity: 1024);
            var total = 0;

            // naive CSV parse (comma-only, no quoting in v1)
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cells = line.Split(',');
                var dict = new Dictionary<string, string?>();
                for (int i = 0; i < headers.Count; i++)
                {
                    var v = (i < cells.Length ? cells[i] : null)?.Trim();
                    dict[headers[i]] = v;
                }

                rowsBuffer.Add(new Features.CampaignModule.Models.CsvRow
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    // 🔁 If your property is not RowJson, change this to the correct one (e.g., DataJson)
                    RowJson = Newtonsoft.Json.JsonConvert.SerializeObject(dict),
                    CreatedAt = DateTime.UtcNow
                });

                total++;

                // chunked insert every 1k for memory safety
                if (rowsBuffer.Count >= 1000)
                {
                    await _db.CsvRows.AddRangeAsync(rowsBuffer, ct);
                    await _db.SaveChangesAsync(ct);
                    rowsBuffer.Clear();
                }
            }

            if (rowsBuffer.Count > 0)
            {
                await _db.CsvRows.AddRangeAsync(rowsBuffer, ct);
            }

            // update batch row count
            var batchRow = await _db.CsvBatches.FirstAsync(b => b.Id == batchId, ct);
            batchRow.RowCount = total;

            await _db.SaveChangesAsync(ct);

            Log.Information("📥 CSV imported | biz={Biz} batch={Batch} rows={Rows} file={File}",
                businessId, batchId, total, fileName);

            return new CsvImportResponseDto
            {
                BatchId = batchId,
                RowCount = total,
                Columns = headers,
                CreatedAt = now
            };
        }
    }
}
