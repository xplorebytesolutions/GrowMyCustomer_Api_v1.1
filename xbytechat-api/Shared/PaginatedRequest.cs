namespace xbytechat.api.Shared
{
    public class PaginatedRequest
    {
        public int Page { get; set; } = 1;       // Page number (1-based)
        public int PageSize { get; set; } = 10;  // Items per page

        // Optional filter (can be extended later)
        public string? Status { get; set; }
        public string? Search { get; set; }
    }
}

