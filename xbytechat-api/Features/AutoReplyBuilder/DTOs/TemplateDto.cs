namespace xbytechat.api.Features.TemplateMessages.DTOs
{
    public class TemplateDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Placeholders { get; set; }
    }
}
