using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    public sealed class UnassignConversationDto
    {
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }
    }
}

