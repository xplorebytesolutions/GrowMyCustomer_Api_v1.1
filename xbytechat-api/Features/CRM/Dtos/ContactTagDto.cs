namespace xbytechat.api.Features.CRM.Dtos
{
    public class ContactTagDto
    {
        public Guid TagId { get; set; }
        public string TagName { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
        public string? Category { get; set; }
    }
}
