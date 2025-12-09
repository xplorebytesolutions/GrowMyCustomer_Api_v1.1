namespace xbytechat.api.Features.Tracking.DTOs
{
    public sealed class MessageLogFacetsDto
    {
        public string[] WabaIds { get; init; } = Array.Empty<string>();     // WhatsAppBusinessNumber
        public string[] SenderIds { get; init; } = Array.Empty<string>();   // Campaign.PhoneNumberId
        public string[] Channels { get; init; } = Array.Empty<string>();    // provider (e.g., META, PINNACLE)
        public string[] Statuses { get; init; } = Array.Empty<string>();    // message status
    }
}
