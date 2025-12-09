using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace xbytechat.api.Features.MessageLogging.Services 
{
    /// Background batch writer for MessageLogs.
    public sealed class MessageLogSink : BackgroundService, IMessageLogSink
    {
        private readonly Channel<MessageLog> _channel =
            Channel.CreateBounded<MessageLog>(new BoundedChannelOptions(20_000)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        private readonly IServiceProvider _sp;
        private const int BatchSize = 1000;

        public MessageLogSink(IServiceProvider sp) => _sp = sp;

        public void Enqueue(MessageLog row) => _channel.Writer.TryWrite(row);

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var batch = new List<MessageLog>(BatchSize);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    while (batch.Count < BatchSize && _channel.Reader.TryRead(out var x))
                        batch.Add(x);

                    if (batch.Count == 0)
                    {
                        var first = await _channel.Reader.ReadAsync(ct);
                        batch.Add(first);
                    }

                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.ChangeTracker.AutoDetectChangesEnabled = false;

                    await db.MessageLogs.AddRangeAsync(batch, ct);
                    await db.SaveChangesAsync(ct);
                    batch.Clear();
                }
                catch (TaskCanceledException) { /* shutdown */ }
                catch { await Task.Delay(200, ct); }
            }
        }
    }
}
