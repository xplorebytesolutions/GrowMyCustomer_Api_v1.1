using System;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.DTOs.Messages
{
    public abstract class BaseMessageDto
    {
        [Required]
        public Guid BusinessId { get; set; }

        [Required]
        [Phone]
        public string RecipientNumber { get; set; }

        public abstract string MessageContent { get; set; }

        //[Required]
        //public string MessageType { get; set; }
    }
}
