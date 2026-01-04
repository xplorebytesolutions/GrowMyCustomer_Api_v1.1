using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    public sealed class SetConversationStatusDto
    {
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

