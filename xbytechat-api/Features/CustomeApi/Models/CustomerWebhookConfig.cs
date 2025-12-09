using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CustomeApi.Models
{
    public class CustomerWebhookConfig
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid BusinessId { get; set; }

        [Required, MaxLength(1024)]
        public string Url { get; set; } = default!;  // customer API endpoint to receive CTAJourney

        [MaxLength(2048)]
        public string? BearerToken { get; set; }     // optional "Authorization: Bearer <token>"

        public bool IsActive { get; set; } = true;

        [Required] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
