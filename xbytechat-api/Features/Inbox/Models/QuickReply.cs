using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.Inbox.Models
{
    public enum QuickReplyScope { Personal = 0, Business = 2 }

    [Table("QuickReplies")]
    public class QuickReply
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid BusinessId { get; set; }
        public Guid? OwnerUserId { get; set; }                // null for Business scope

        [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
        [Required] public string Body { get; set; } = string.Empty;

        [MaxLength(240)] public string? TagsCsv { get; set; }
        [MaxLength(8)] public string? Language { get; set; }   // e.g. "en", "hi"

        public QuickReplyScope Scope { get; set; } = QuickReplyScope.Personal;
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
