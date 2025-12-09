using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.BusinessModule.DTOs
{
    public class SignupBusinessDto
    {
        [Required]
        public string CompanyName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public string? RepresentativeName { get; set; }

        public string? Phone { get; set; }
        public string RoleName { get; set; } = "business"; // Default to business role

        // 🆕 NEW FIELD (Internal use only)
        public Guid? CreatedByPartnerId { get; set; } // to assign the business to a specific user/agent/partner}

    }
}
