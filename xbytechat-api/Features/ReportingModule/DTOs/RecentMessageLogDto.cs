// 📄 File: Features/ReportingModule/DTOs/RecentMessageLogDto.cs
using System;

namespace xbytechat.api.Features.ReportingModule.DTOs
{
    public class RecentMessageLogDto
    {
        public Guid Id { get; set; }
        public string RecipientNumber { get; set; }
        public string MessageContent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }             
        public Guid? CampaignId { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }         
    }
}

