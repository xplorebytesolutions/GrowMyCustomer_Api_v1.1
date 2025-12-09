namespace xbytechat.api.Features.Inbox.DTOs
{
    public class SendMessageInputDto
    {
        public Guid ContactId { get; set; } // 🔁 REMOVE the "?" (nullable) unless needed
        public string Message { get; set; } = string.Empty;
    }
}
