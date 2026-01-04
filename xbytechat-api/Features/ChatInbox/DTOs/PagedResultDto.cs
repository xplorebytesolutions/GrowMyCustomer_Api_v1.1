using System.Collections.Generic;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    public sealed class PagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }
}
