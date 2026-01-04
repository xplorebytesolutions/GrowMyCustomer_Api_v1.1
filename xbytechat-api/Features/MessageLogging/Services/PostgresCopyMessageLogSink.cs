using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using xbytechat.api.Features.CampaignTracking.Models; // MessageLog
using xbytechat.api.AuthModule.Models;               // AppDbContext

namespace xbytechat.api.Features.MessageLogging.Services;

/// <summary>
/// Background sink that batches MessageLogs and writes with COPY BINARY into Postgres.
/// Falls back to EF AddRange on error or when UseCopy=false.
/// </summary>
public sealed class PostgresCopyMessageLogSink : BackgroundService, IMessageLogSink
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<PostgresCopyMessageLogSink> _log;
    private readonly MessageLogSinkOptions _opt;
    private readonly Channel<MessageLog> _ch;

    public PostgresCopyMessageLogSink(
        IServiceProvider sp,
        ILogger<PostgresCopyMessageLogSink> log,
        IOptions<MessageLogSinkOptions> opt)
    {
        _sp = sp;
        _log = log;
        _opt = opt?.Value ?? new MessageLogSinkOptions();

        _ch = Channel.CreateBounded<MessageLog>(new BoundedChannelOptions(capacity: 50_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _log.LogInformation("MessageLog sink initialized (BatchSize={Batch}, FlushIntervalMs={Flush}, UseCopy={UseCopy})",
            _opt.BatchSize, _opt.FlushIntervalMs, _opt.UseCopy);
    }

    public void Enqueue(MessageLog row)
    {
        // Fast path: try non-blocking write.
        if (!_ch.Writer.TryWrite(row))
        {
            // Channel is full or not ready: enqueue asynchronously without blocking the caller.
            _ = _ch.Writer.WriteAsync(row);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<MessageLog>(_opt.BatchSize);
        var flushDelay = TimeSpan.FromMilliseconds(Math.Max(100, _opt.FlushIntervalMs));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for either new data or idle timeout to flush partial batches
                var readTask = _ch.Reader.WaitToReadAsync(stoppingToken).AsTask();
                var delayTask = Task.Delay(flushDelay, stoppingToken);
                var winner = await Task.WhenAny(readTask, delayTask);

                if (winner == readTask && await readTask)
                {
                    while (_ch.Reader.TryRead(out var row))
                    {
                        batch.Add(row);
                        if (batch.Count >= _opt.BatchSize)
                        {
                            await FlushAsync(batch, stoppingToken);
                            batch.Clear();
                        }
                    }
                }

                // Idle flush if timer fired and we have pending rows
                if (winner == delayTask && batch.Count > 0)
                {
                    await FlushAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _log.LogError(ex, "[MessageLogSink] loop error");
                await Task.Delay(500, stoppingToken);
            }
        }

        // final drain
        try
        {
            if (batch.Count > 0) await FlushAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[MessageLogSink] final flush failed");
        }
    }

    private async Task FlushAsync(List<MessageLog> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _log.LogInformation(
            "[MessageLogSink] Flushing {Count} logs. FirstIds={Ids}",
            rows.Count,
            string.Join(",", rows.Take(5).Select(x => x.Id)));

        if (_opt.UseCopy && db.Database.IsNpgsql())
        {
            var connString = db.Database.GetDbConnection().ConnectionString;

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(ct);

            // ✅ Align with table: both IsChargeable and IsIncoming are present (in this order)
            // ✅ Added ContactId to COPY list (positional order matters)
            // ✅ Also keep it close to BusinessId/CampaignId for clarity
            const string copySql = @"
COPY ""MessageLogs"" (
    ""Id"",
    ""BusinessId"",
    ""CampaignId"",
    ""ContactId"",
    ""RecipientNumber"",
    ""MessageContent"",
    ""MediaUrl"",
    ""Status"",
    ""MessageId"",
    ""ErrorMessage"",
    ""RawResponse"",
    ""CreatedAt"",
    ""SentAt"",
    ""Source"",
    ""RunId"",
    ""Provider"",
    ""ProviderMessageId"",
    ""IsChargeable"",
    ""IsIncoming""
) FROM STDIN (FORMAT BINARY);";


            try
            {
                await using var importer = await conn.BeginBinaryImportAsync(copySql, ct);

                foreach (var r in rows)
                {
                    await importer.StartRowAsync(ct);

                    //importer.Write(r.Id, NpgsqlDbType.Uuid);
                    //importer.Write(r.BusinessId, NpgsqlDbType.Uuid);
                    //importer.Write(r.CampaignId, NpgsqlDbType.Uuid);

                    //if (string.IsNullOrWhiteSpace(r.RecipientNumber)) importer.WriteNull();
                    //else importer.Write(r.RecipientNumber, NpgsqlDbType.Text);
                    importer.Write(r.Id, NpgsqlDbType.Uuid);
                    importer.Write(r.BusinessId, NpgsqlDbType.Uuid);

                    // ✅ CampaignId can be nullable in many systems; write safely
                    if (r.CampaignId.HasValue) importer.Write(r.CampaignId.Value, NpgsqlDbType.Uuid);
                    else importer.WriteNull();

                    // ✅ NEW: ContactId (nullable) — must match COPY column order exactly
                    if (r.ContactId.HasValue) importer.Write(r.ContactId.Value, NpgsqlDbType.Uuid);
                    else importer.WriteNull();

                    if (string.IsNullOrWhiteSpace(r.RecipientNumber)) importer.WriteNull();
                    else importer.Write(r.RecipientNumber, NpgsqlDbType.Text);



                    if (string.IsNullOrWhiteSpace(r.MessageContent)) importer.WriteNull();
                    else importer.Write(r.MessageContent, NpgsqlDbType.Text);

                    if (string.IsNullOrWhiteSpace(r.MediaUrl)) importer.WriteNull();
                    else importer.Write(r.MediaUrl, NpgsqlDbType.Text);

                    if (string.IsNullOrWhiteSpace(r.Status)) importer.WriteNull();
                    else importer.Write(r.Status, NpgsqlDbType.Text);

                    if (string.IsNullOrWhiteSpace(r.MessageId)) importer.WriteNull();
                    else importer.Write(r.MessageId, NpgsqlDbType.Text);

                    if (string.IsNullOrWhiteSpace(r.ErrorMessage)) importer.WriteNull();
                    else importer.Write(r.ErrorMessage, NpgsqlDbType.Text);

                    if (string.IsNullOrWhiteSpace(r.RawResponse)) importer.WriteNull();
                    else importer.Write(r.RawResponse, NpgsqlDbType.Text);

                    importer.Write(r.CreatedAt, NpgsqlDbType.TimestampTz);
                    if (r.SentAt.HasValue) importer.Write(r.SentAt.Value, NpgsqlDbType.TimestampTz);
                    else importer.WriteNull();

                    if (string.IsNullOrWhiteSpace(r.Source)) importer.WriteNull();
                    else importer.Write(r.Source, NpgsqlDbType.Text);

                    importer.Write(r.RunId, NpgsqlDbType.Uuid);

                    if (string.IsNullOrWhiteSpace(r.Provider)) importer.WriteNull();
                    else importer.Write(r.Provider, NpgsqlDbType.Text);

                    if (string.IsNullOrWhiteSpace(r.ProviderMessageId)) importer.WriteNull();
                    else importer.Write(r.ProviderMessageId, NpgsqlDbType.Text);

                    // ✅ keep order in sync with COPY list
                    importer.Write(r.IsChargeable, NpgsqlDbType.Boolean);
                    importer.Write(r.IsIncoming, NpgsqlDbType.Boolean);
                }

                await importer.CompleteAsync(ct);
                _log.LogDebug("[MessageLogSink] COPY inserted {Count} rows", rows.Count);
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "[MessageLogSink] COPY failed, falling back to EF for {Count} rows. FirstIds={Ids}",
                    rows.Count,
                    string.Join(",", rows.Take(5).Select(x => x.Id)));
                // fall through to EF path
            }
        }

        // Fallback EF insert
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var written = await db.BulkInsertMessageLogsAsync(rows, ct);
            var ids = rows.Select(x => x.Id).ToList();

            if (written != rows.Count)
            {
                _log.LogError(
                    "[MessageLogSink] EF insert wrote {Written}/{Count} rows. FirstIds={Ids}",
                    written, rows.Count, string.Join(",", ids.Take(5)));
            }

            // Verify all IDs are present; if not, try once more for missing rows
            var existingIds = await db.MessageLogs
                .AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync(ct);

            var missing = ids.Except(existingIds).ToList();
            if (missing.Count > 0)
            {
                _log.LogError(
                    "[MessageLogSink] EF verification missing {Missing} rows. MissingIds={Ids}",
                    missing.Count, string.Join(",", missing));

                var missingRows = rows.Where(r => missing.Contains(r.Id)).ToList();
                db.MessageLogs.AddRange(missingRows);
                var retryWritten = await db.SaveChangesAsync(ct);

                var retryExisting = await db.MessageLogs
                    .AsNoTracking()
                    .Where(m => ids.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync(ct);

                var stillMissing = ids.Except(retryExisting).ToList();
                if (stillMissing.Count > 0)
                {
                    _log.LogError(
                        "[MessageLogSink] EF insert retry still missing {Missing} rows. MissingIds={Ids}",
                        stillMissing.Count, string.Join(",", stillMissing));
                    throw new InvalidOperationException("MessageLogSink failed to persist MessageLogs: " + string.Join(",", stillMissing));
                }

                _log.LogInformation(
                    "[MessageLogSink] EF retry inserted missing rows. Written={Written} RetryWritten={RetryWritten}",
                    written, retryWritten);
            }
            else
            {
                _log.LogInformation(
                    "[MessageLogSink] EF insert succeeded for {Count} rows. FirstIds={Ids}",
                    rows.Count, string.Join(",", ids.Take(5)));
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "[MessageLogSink] EF insert failed for {Count} rows. FirstIds={Ids}",
                rows.Count,
                string.Join(",", rows.Take(5).Select(x => x.Id)));
            throw;
        }
    }

}

/// <summary>
/// Small EF helper for fallback insert path (kept here for locality).
/// </summary>
internal static class MessageLogEfFallback
{
    public static async Task<int> BulkInsertMessageLogsAsync(this AppDbContext db, IEnumerable<MessageLog> rows, CancellationToken ct)
    {
        db.MessageLogs.AddRange(rows);
        return await db.SaveChangesAsync(ct);
    }
}
