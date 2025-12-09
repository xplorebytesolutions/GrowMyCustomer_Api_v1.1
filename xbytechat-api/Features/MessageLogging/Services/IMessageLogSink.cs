namespace xbytechat.api.Features.MessageLogging.Services
{
    /// Fire-and-forget enqueue API for MessageLog rows.
    public interface IMessageLogSink
    {
        void Enqueue(MessageLog row);
    }
}
