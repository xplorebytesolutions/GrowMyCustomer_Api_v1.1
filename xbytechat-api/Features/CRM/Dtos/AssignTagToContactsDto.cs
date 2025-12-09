namespace xbytechat.api.Features.CRM.Dtos
{
    public class AssignTagToContactsDto
    {
        public List<Guid> ContactIds { get; set; } = new();
        public Guid TagId { get; set; }
    }
}
