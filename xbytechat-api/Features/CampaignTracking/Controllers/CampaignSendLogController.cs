using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignTracking.DTOs;
using xbytechat.api.Features.CampaignTracking.Services;

namespace xbytechat.api.Features.CampaignTracking.Controllers
{
    [ApiController]
    [Route("api/campaign-logs")]
    public class CampaignSendLogController : ControllerBase
    {
        private readonly ICampaignSendLogService _logService;
        private readonly ICampaignTrackingRetryService _retryService;

        public CampaignSendLogController(
            ICampaignSendLogService logService,
            ICampaignTrackingRetryService retryService)
        {
            _logService = logService;
            _retryService = retryService;
        }

        [HttpGet("campaign/{campaignId}")]
        public async Task<IActionResult> GetLogsByCampaign(
            Guid campaignId,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _logService.GetLogsByCampaignIdAsync(campaignId, status, search, page, pageSize);
            return Ok(result);
        }

        [HttpGet("campaign/{campaignId}/contact/{contactId}")]
        public async Task<IActionResult> GetLogsForContact(Guid campaignId, Guid contactId)
        {
            var logs = await _logService.GetLogsForContactAsync(campaignId, contactId);
            return Ok(logs);
        }

        [HttpPost]
        public async Task<IActionResult> AddSendLog([FromBody] CampaignSendLogDto dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString() ?? "unknown";

            var result = await _logService.AddSendLogAsync(dto, ipAddress, userAgent);
            if (!result)
                return BadRequest(new { message = "Failed to add send log" });

            return Ok(new { success = true });
        }

        [HttpPut("{logId}/status")]
        public async Task<IActionResult> UpdateDeliveryStatus(Guid logId, [FromBody] DeliveryStatusUpdateDto dto)
        {
            var result = await _logService.UpdateDeliveryStatusAsync(logId, dto.Status, dto.DeliveredAt, dto.ReadAt);
            if (!result)
                return NotFound(new { message = "Log not found" });

            return Ok(new { success = true });
        }

        [HttpPut("{logId}/track-click")]
        public async Task<IActionResult> TrackClick(Guid logId, [FromBody] ClickTrackDto dto)
        {
            var result = await _logService.TrackClickAsync(logId, dto.ClickType);
            if (!result)
                return NotFound(new { message = "Log not found" });

            return Ok(new { success = true });
        }

        [HttpPost("{logId}/retry")]
        public async Task<IActionResult> RetrySingle(Guid logId)
        {
            var result = await _retryService.RetrySingleAsync(logId);
            if (!result)
                return BadRequest(new { message = "Retry failed" });

            return Ok(new { success = true });
        }

        [HttpPost("campaign/{campaignId}/retry-all")]
        public async Task<IActionResult> RetryAll(Guid campaignId)
        {
            var result = await _retryService.RetryFailedInCampaignAsync(campaignId);
            return Ok(new { success = true, retried = result });
        }

        [HttpGet("campaign/{campaignId}/summary")]
        public async Task<IActionResult> GetCampaignSummary(
            Guid campaignId,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int repliedWindowDays = 7,
            [FromQuery] Guid? runId = null)
        {
            if (repliedWindowDays < 0) repliedWindowDays = 0;
            if (repliedWindowDays > 90) repliedWindowDays = 90;

            var summary = await _logService.GetCampaignSummaryAsync(
                campaignId: campaignId,
                fromUtc: fromUtc,
                toUtc: toUtc,
                repliedWindowDays: repliedWindowDays,
                runId: runId);

            return Ok(summary);
        }

        [HttpGet("campaign/{campaignId}/contacts")]
        public async Task<IActionResult> GetContactsByBucket(
            Guid campaignId,
            [FromQuery] string bucket = "sent",
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] int repliedWindowDays = 7,
            [FromQuery] Guid? runId = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _logService.GetContactsByStatBucketAsync(
                campaignId,
                bucket,
                fromUtc,
                toUtc,
                repliedWindowDays,
                runId,
                search,
                page,
                pageSize);

            return Ok(result);
        }
    }

    public class DeliveryStatusUpdateDto
    {
        public string Status { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class ClickTrackDto
    {
        public string ClickType { get; set; }
    }
}
