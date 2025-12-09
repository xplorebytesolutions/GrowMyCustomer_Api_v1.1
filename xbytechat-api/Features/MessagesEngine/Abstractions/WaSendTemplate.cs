using System;

namespace xbytechat.api.Features.MessagesEngine.Abstractions
{
    public class WaSendTemplate
    {
        public Guid BusinessId { get; init; }
        public string To { get; init; } = "";
        public string TemplateName { get; init; } = "";
        public string Language { get; init; } = "en_US";
        public object? Components { get; init; }
    }
}
