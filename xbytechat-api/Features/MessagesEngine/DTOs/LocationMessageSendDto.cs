using System;

namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    public sealed class LocationMessageSendDto
    {
        public Guid BusinessId { get; set; }
        public string RecipientNumber { get; set; } = string.Empty;

        public Guid ContactId { get; set; }
        public string? PhoneNumberId { get; set; }
        public string? Provider { get; set; } // defaults to business sender; keep for parity with other DTOs
        public string? Source { get; set; }   // "agent" | "automation" | etc.

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
    }
}

