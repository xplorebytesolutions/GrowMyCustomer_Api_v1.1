using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace xbytechat.api.Features.CampaignModule.DTOs.Requests
{
    // Swagger-safe wrapper for multipart upload
    public sealed class CsvBatchUploadForm
    {
        public Guid? AudienceId { get; set; }

        [Required]
        public IFormFile File { get; set; } = default!;
    }
}
