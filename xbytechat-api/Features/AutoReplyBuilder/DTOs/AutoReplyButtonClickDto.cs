namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    public class AutoReplyButtonClickDto
    {
        public Guid FlowId { get; set; }
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string ButtonText { get; set; } = string.Empty;
        public Guid? RefMessageId { get; set; }

    }
}
