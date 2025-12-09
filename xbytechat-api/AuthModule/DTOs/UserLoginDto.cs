using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.AuthModule.DTOs
{
    public class UserLoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
