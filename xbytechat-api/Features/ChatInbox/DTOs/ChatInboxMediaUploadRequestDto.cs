using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace xbytechat.api.Features.ChatInbox.DTOs
{
    public sealed class ChatInboxMediaUploadRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}

