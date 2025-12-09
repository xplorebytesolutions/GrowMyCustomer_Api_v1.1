namespace xbytechat.api.Features.BusinessModule.DTOs
{
    public class PendingBusinessDto
    {
        public Guid BusinessId { get; set; }
        public string CompanyName { get; set; }
        public string BusinessEmail { get; set; }
        public string? RepresentativeName { get; set; }
        public string? Phone { get; set; }
        public string Plan { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool? IsApproved { get; set; }
    }
}
