namespace xbytechat.api.Features.ChatInbox.DTOs
{
    public sealed class ChatInboxMediaUploadResponseDto
    {
        public string MediaId { get; set; } = string.Empty;

        /// <summary>
        /// "image" | "document"
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }
}

