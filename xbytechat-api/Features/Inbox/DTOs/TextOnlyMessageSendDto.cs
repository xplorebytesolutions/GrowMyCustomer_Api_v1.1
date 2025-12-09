namespace xbytechat.api.Features.Inbox.DTOs
{
    public class TextOnlyMessageSendDto
    {
        public Guid BusinessId { get; set; }

        public string RecipientNumber { get; set; }

        public string TextContent { get; set; }
    }
}
