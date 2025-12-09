using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.AuthModule.DTOs
{
    public class ResendConfirmationDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}

