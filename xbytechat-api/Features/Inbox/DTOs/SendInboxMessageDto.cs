using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.Inbox.DTOs
{
    /// <summary>
    /// DTO sent from frontend when a user sends a new message.
    /// </summary>
    public class SendInboxMessageDto
    {
        [Required]
        public Guid ContactId { get; set; }

        [Required]
        public string MessageBody { get; set; }

        public string? MediaUrl { get; set; } // Optional image or file
    }
}
