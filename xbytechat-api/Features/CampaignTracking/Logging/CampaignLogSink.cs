using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using xbytechat.api.Features.CampaignTracking.Models; // CampaignSendLog
using xbytechat.api.AuthModule.Models;               // AppDbContext

namespace xbytechat.api.Features.CampaignTracking.Logging
{
    public class CampaignLogSink : ICampaignLogSink
    {
        private readonly ConcurrentQueue<CampaignLogRecord> _queue = new();
        private readonly ConcurrentDictionary<Guid, int> _attempts = new();
        private readonly ILogger<CampaignLogSink> _log;
        private readonly IServiceProvider _sp;
        private readonly IOptionsMonitor<BatchingOptions> _opts;

        private const int MaxAttempts = 3;

        public CampaignLogSink(ILogger<CampaignLogSink> log, IServiceProvider sp, IOptionsMonitor<BatchingOptions> opts)
        {
            _log = log; _sp = sp; _opts = opts;
        }

        public void Enqueue(CampaignLogRecord rec) => _queue.Enqueue(rec);
        public int PendingCount => _queue.Count;

        public async Task FlushAsync(CancellationToken ct = default)
        {
            var max = _opts.CurrentValue.CampaignLog.MaxBatchSize;
            var list = new List<CampaignLogRecord>(Math.Min(_queue.Count, max));
            while (list.Count < max && _queue.TryDequeue(out var r)) list.Add(r);
            if (list.Count == 0) return;

            try
            {
                // Ensure all referenced MessageLogs exist before inserting send logs
                var messageLogIds = list
                    .Select(x => x.MessageLogId)
                    .Where(id => id.HasValue && id.Value != Guid.Empty)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                _log.LogInformation(
                    "[CampaignLogSink] Batch size = {BatchCount}, messageLogIds = {IdCount}",
                    list.Count, messageLogIds.Count);

                if (messageLogIds.Count > 0)
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var existingIds = await db.MessageLogs
                        .AsNoTracking()
                        .Where(m => messageLogIds.Contains(m.Id))
                        .Select(m => m.Id)
                        .ToListAsync(ct);

                    var missing = messageLogIds.Except(existingIds).ToList();
                    if (missing.Count > 0)
                    {
                        // Use first record Id as batch key
                        var batchKey = list[0].Id;
                        var attempt = _attempts.AddOrUpdate(batchKey, 1, (_, prev) => prev + 1);

                        if (attempt <= MaxAttempts)
                        {
                            _log.LogWarning(
                                "[CampaignLogSink] Deferring batch (attempt {Attempt}/{Max}) – messageLogIds={Ids} missingCount={MissingCount}",
                                attempt, MaxAttempts, string.Join(",", messageLogIds), missing.Count);
                            foreach (var item in list) _queue.Enqueue(item);
                            return;
                        }

                        _log.LogError(
                            "[CampaignLogSink] Dropping batch after {MaxAttempts} attempts – still missing messageLogIds={Ids}",
                            MaxAttempts, string.Join(",", messageLogIds));
                        _attempts.TryRemove(batchKey, out _);
                        return; // drop to avoid FK violations / infinite loop
                    }

                    // All required MessageLogs exist; clear attempts for this batch key
                    _attempts.TryRemove(list[0].Id, out _);
                }

                if (_opts.CurrentValue.CampaignLog.UseCopy)
                    await CopyInsertAsync(list, ct);
                else
                    await EfInsertAsync(list, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[CampaignLogSink] COPY failed; falling back to EF");
                try { await EfInsertAsync(list, ct); }
                catch (Exception ex2)
                {
                    _log.LogError(ex2, "[CampaignLogSink] EF fallback failed; requeueing {Count}", list.Count);
                    foreach (var rr in list) _queue.Enqueue(rr);
                }
            }
        }

        private static void WriteNullable<T>(NpgsqlBinaryImporter w, T? value, NpgsqlDbType type) where T : struct
        {
            if (value.HasValue) w.Write(value.Value, type);
            else w.WriteNull();
        }

        private static void WriteNullableText(NpgsqlBinaryImporter w, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) w.WriteNull();
            else w.Write(value, NpgsqlDbType.Text);
        }

        private static void WriteNullableVarchar(NpgsqlBinaryImporter w, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) w.WriteNull();
            else w.Write(value, NpgsqlDbType.Varchar);
        }

        private static void WriteNullableUuid(NpgsqlBinaryImporter w, Guid? value)
        {
            if (value.HasValue && value.Value != Guid.Empty) w.Write(value.Value, NpgsqlDbType.Uuid);
            else w.WriteNull();
        }

        private async Task CopyInsertAsync(List<CampaignLogRecord> batch, CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connString = db.Database.GetDbConnection().ConnectionString;

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(ct);

            const string sql = @"
COPY ""CampaignSendLogs"" (
    ""Id"",
    ""RunId"",
    ""MessageId"",
    ""CampaignId"",
    ""ContactId"",
    ""RecipientId"",
    ""MessageBody"",
    ""TemplateId"",
    ""SendStatus"",
    ""ErrorMessage"",
    ""CreatedAt"",
    ""CreatedBy"",
    ""SentAt"",
    ""DeliveredAt"",
    ""ReadAt"",
    ""IpAddress"",
    ""DeviceInfo"",
    ""MacAddress"",
    ""SourceChannel"",
    ""DeviceType"",
    ""Browser"",
    ""Country"",
    ""City"",
    ""IsClicked"",
    ""ClickedAt"",
    ""ClickType"",
    ""RetryCount"",
    ""LastRetryAt"",
    ""LastRetryStatus"",
    ""AllowRetry"",
    ""MessageLogId"",
    ""BusinessId"",
    ""CTAFlowConfigId"",
    ""CTAFlowStepId"",
    ""ButtonBundleJson""
) FROM STDIN (FORMAT BINARY);";

            try
            {
                await using var writer = await conn.BeginBinaryImportAsync(sql, ct);

                foreach (var r in batch)
                {
                    await writer.StartRowAsync(ct);

                    // Required IDs
                    writer.Write(r.Id, NpgsqlDbType.Uuid);
                    writer.Write(r.RunId, NpgsqlDbType.Uuid);

                    // Strings / nullable fields
                    WriteNullableVarchar(writer, r.MessageId);
                    writer.Write(r.CampaignId, NpgsqlDbType.Uuid);
                    WriteNullableUuid(writer, r.ContactId);
                    WriteNullableUuid(writer, r.RecipientId);

                    WriteNullableText(writer, r.MessageBody);
                    WriteNullableVarchar(writer, r.TemplateId);
                    WriteNullableVarchar(writer, r.SendStatus);
                    WriteNullableVarchar(writer, r.ErrorMessage);

                    // Timestamps
                    writer.Write(r.CreatedAt, NpgsqlDbType.TimestampTz);
                    WriteNullableVarchar(writer, r.CreatedBy);
                    WriteNullable(writer, r.SentAt, NpgsqlDbType.TimestampTz);
                    WriteNullable(writer, r.DeliveredAt, NpgsqlDbType.TimestampTz);
                    WriteNullable(writer, r.ReadAt, NpgsqlDbType.TimestampTz);

                    // Device / network
                    WriteNullableVarchar(writer, r.IpAddress);
                    WriteNullableVarchar(writer, r.DeviceInfo);
                    WriteNullableVarchar(writer, r.MacAddress);
                    WriteNullableVarchar(writer, r.SourceChannel);
                    WriteNullableVarchar(writer, r.DeviceType);
                    WriteNullableVarchar(writer, r.Browser);
                    WriteNullableVarchar(writer, r.Country);
                    WriteNullableVarchar(writer, r.City);

                    // Click info
                    writer.Write(r.IsClicked, NpgsqlDbType.Boolean);
                    WriteNullable(writer, r.ClickedAt, NpgsqlDbType.TimestampTz);
                    WriteNullableVarchar(writer, r.ClickType);

                    // Retry info — RetryCount is non-nullable int in your model
                    writer.Write(r.RetryCount, NpgsqlDbType.Integer);
                    WriteNullable(writer, r.LastRetryAt, NpgsqlDbType.TimestampTz);
                    WriteNullableVarchar(writer, r.LastRetryStatus);
                    writer.Write(r.AllowRetry, NpgsqlDbType.Boolean);

                    // FK to MessageLogs may be null initially
                    WriteNullableUuid(writer, r.MessageLogId);

                    // Remaining Ids
                    writer.Write(r.BusinessId, NpgsqlDbType.Uuid);
                    WriteNullableUuid(writer, r.CTAFlowConfigId);
                    WriteNullableUuid(writer, r.CTAFlowStepId);

                    // Bundle (text/json)
                    WriteNullableText(writer, r.ButtonBundleJson);
                }

                await writer.CompleteAsync(ct);
                _log.LogDebug("[CampaignLogSink] COPY inserted {Count} rows", batch.Count);
            }
            catch
            {
                throw; // let FlushAsync() handle fallback/requeue
            }
        }

        private async Task EfInsertAsync(List<CampaignLogRecord> batch, CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entities = batch.Select(r => new CampaignSendLog
            {
                Id = r.Id,
                RunId = r.RunId,
                MessageId = r.MessageId,
                CampaignId = r.CampaignId,
                ContactId = r.ContactId,
                RecipientId = r.RecipientId,
                MessageBody = r.MessageBody,
                TemplateId = r.TemplateId,
                SendStatus = r.SendStatus,
                ErrorMessage = r.ErrorMessage,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy,
                SentAt = r.SentAt,
                DeliveredAt = r.DeliveredAt,
                ReadAt = r.ReadAt,
                IpAddress = r.IpAddress,
                DeviceInfo = r.DeviceInfo,
                MacAddress = r.MacAddress,
                SourceChannel = r.SourceChannel,
                DeviceType = r.DeviceType,
                Browser = r.Browser,
                Country = r.Country,
                City = r.City,
                IsClicked = r.IsClicked,
                ClickedAt = r.ClickedAt,
                ClickType = r.ClickType,
                RetryCount = r.RetryCount,
                LastRetryAt = r.LastRetryAt,
                LastRetryStatus = r.LastRetryStatus,
                AllowRetry = r.AllowRetry,
                MessageLogId = r.MessageLogId,
                BusinessId = r.BusinessId,
                CTAFlowConfigId = r.CTAFlowConfigId,
                CTAFlowStepId = r.CTAFlowStepId,
                ButtonBundleJson = r.ButtonBundleJson
            }).ToList();

            var prev = db.ChangeTracker.AutoDetectChangesEnabled;
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            await db.CampaignSendLogs.AddRangeAsync(entities, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.AutoDetectChangesEnabled = prev;
        }
    }

    public class BatchingOptions
    {
        public CampaignLogOptions CampaignLog { get; set; } = new();
        public class CampaignLogOptions
        {
            public int FlushEveryMs { get; set; } = 500;
            public int MaxBatchSize { get; set; } = 500;
            public bool UseCopy { get; set; } = true;
        }
    }
}
