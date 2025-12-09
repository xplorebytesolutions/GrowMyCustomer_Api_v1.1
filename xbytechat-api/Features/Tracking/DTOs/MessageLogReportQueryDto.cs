namespace xbytechat.api.Features.Tracking.DTOs
{
    public sealed class MessageLogReportQueryDto
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public string? Search { get; set; }                 // phone or text contains
        public string[]? Statuses { get; set; }             // Sent/Delivered/Read/Failed etc.
        public string[]? Channels { get; set; }             // meta_cloud/sms/email…
        public string[]? SenderIds { get; set; }            // phone_number_id
        public string[]? MessageTypes { get; set; }         // text/image/template…
        public Guid? CampaignId { get; set; }               // optional scope

        public string? SortBy { get; set; } = "SentAt";     // server-whitelisted
        public string? SortDir { get; set; } = "desc";      // asc|desc

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public sealed class MessageLogListItemDto
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid? CampaignId { get; set; }
        public string? CampaignName { get; set; }

        public string? RecipientNumber { get; set; }
        public string? SenderId { get; set; }
        public string? SourceChannel { get; set; }
        public string? Status { get; set; }
        public string? MessageType { get; set; }

        public string? MessageContent { get; set; }
        public string? TemplateName { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
