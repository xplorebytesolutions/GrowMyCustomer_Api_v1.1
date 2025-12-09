using System.Collections.Generic;

namespace xbytechat.api.Features.MessagesEngine.Contracts
{
    /// <summary>
    /// Provider-agnostic message envelope. Maps to Meta/Pinbot under the hood.
    /// </summary>
    public sealed record MessageEnvelope(
        string To,
        string Kind,                       // "text" | "template" | "interactive"
        string? TemplateName = null,
        string LanguageCode = "en_US",
        List<object>? Components = null,    // template components
        object? Interactive = null,         // interactive payload (if any)
        string? TextBody = null,
        string? ImageUrl = null
    );
}
