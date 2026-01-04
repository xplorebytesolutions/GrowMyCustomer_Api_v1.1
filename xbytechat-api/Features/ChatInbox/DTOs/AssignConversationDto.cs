using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    public sealed class AssignConversationDto
    {
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }
        public Guid UserId { get; set; }
    }
}

