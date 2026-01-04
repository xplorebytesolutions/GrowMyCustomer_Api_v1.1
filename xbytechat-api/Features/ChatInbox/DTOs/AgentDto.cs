using System;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    public sealed class AgentDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? RoleName { get; set; }
    }
}

