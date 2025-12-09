namespace xbytechat.api.Features.MessageLogging.Services;

public sealed class MessageLogSinkOptions
{
    // rows per COPY
    public int BatchSize { get; set; } = 1000;

    // flush if idle for this long (ms)
    public int FlushIntervalMs { get; set; } = 800;

    // fallback to EF if false
    public bool UseCopy { get; set; } = true;
}
