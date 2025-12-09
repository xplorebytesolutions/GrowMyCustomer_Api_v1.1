namespace xbytechat.api.Features.MessagesEngine.DTOs
{
    public class AutoSendTemplateMessageDto
    {
        public Guid BusinessId { get; set; }
        public Guid ContactId { get; set; }
        public string PhoneNumber { get; set; }
        public Guid TemplateId { get; set; }
        public string TemplateName { get; set; }
        public Dictionary<string, string> Placeholders { get; set; } = new();
    }
}
