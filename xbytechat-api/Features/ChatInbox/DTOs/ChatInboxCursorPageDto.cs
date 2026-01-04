// 📄 xbytechat-api/Features/ChatInbox/DTOs/ChatInboxCursorPageDto.cs
using System.Collections.Generic;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    /// <summary>
    /// Cursor-based page response (stable for chat inbox).
    /// </summary>
    public sealed class ChatInboxCursorPageDto<T>
    {
        public IReadOnlyList<T> Items { get; init; } = new List<T>();
        public string? NextCursor { get; init; }
        public bool HasMore { get; init; }
        public int Limit { get; init; }
    }
}
