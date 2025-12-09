using System;

namespace xbytechat.api.Features.MessagesEngine.Abstractions
{
    public class WaSendText
    {
        public Guid BusinessId { get; init; }
        public string To { get; init; } = "";
        public string Body { get; init; } = "";
    }
}
