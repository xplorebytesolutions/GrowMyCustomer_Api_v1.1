namespace xbytechat.api.Features.BusinessModule.DTOs
{
    /// <summary>
    /// Allowed fields for business profile updates.
    /// No lifecycle / plan / ownership fields here.
    /// </summary>
    public class UpdateBusinessDto
    {
        public string? CompanyName { get; set; }
        public string? BusinessName { get; set; }
        public string? BusinessEmail { get; set; }
        public string? Phone { get; set; }
        public string? CompanyPhone { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }
        public string? Industry { get; set; }
        public string? LogoUrl { get; set; }
        public string? Tags { get; set; }
        public string? Notes { get; set; }
    }
}
