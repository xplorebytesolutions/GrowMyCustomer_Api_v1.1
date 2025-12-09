using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.Inbox.Models;

namespace xbytechat.api.Features.Inbox.DTOs
{
    public sealed class QuickReplyDto
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid? OwnerUserId { get; set; }
        public QuickReplyScope Scope { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? TagsCsv { get; set; }
        public string? Language { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class QuickReplyCreateDto
    {
        [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
        [Required] public string Body { get; set; } = string.Empty;
        [MaxLength(240)] public string? TagsCsv { get; set; }
        [MaxLength(8)] public string? Language { get; set; }
        public QuickReplyScope Scope { get; set; } = QuickReplyScope.Personal;
    }

    public sealed class QuickReplyUpdateDto
    {
        [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
        [Required] public string Body { get; set; } = string.Empty;
        [MaxLength(240)] public string? TagsCsv { get; set; }
        [MaxLength(8)] public string? Language { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
