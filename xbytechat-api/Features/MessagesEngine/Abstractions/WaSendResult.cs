using System.Net;

namespace xbytechat.api.Features.MessagesEngine.Abstractions
{
    public record WaSendResult(
        bool Success,
        string Provider,
        string? ProviderMessageId = null,
        HttpStatusCode? StatusCode = null,
        string? RawResponse = null,
        string? Error = null
    );
}
