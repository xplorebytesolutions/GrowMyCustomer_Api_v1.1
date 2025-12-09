using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api; // Your using for AppDbContext
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Features.CampaignTracking.Worker; // Your using for DTOs

namespace xbytechat.api.Features.Tracking.Controllers
{
    [ApiController]
    [Route("api/tracking")]
    public class TrackingController : ControllerBase
    {
        private readonly ITrackingService _tracker;
        private readonly AppDbContext _context;
        private readonly IContactJourneyService _journeyService;
        private readonly IJourneyExportService _journeyExport;
        public TrackingController(ITrackingService tracker, AppDbContext context, IContactJourneyService journeyService, IJourneyExportService journeyExport)
        {
            _tracker = tracker;
            _context = context;
            _journeyService = journeyService;
            _journeyExport = journeyExport;
        }

        [HttpGet("journeys/{campaignSendLogId}")]
        public async Task<IActionResult> GetJourney(Guid campaignSendLogId)
        {
            var journeyEvents = await _journeyService.GetJourneyEventsAsync(campaignSendLogId);
            return Ok(journeyEvents);
        }


        //[HttpGet("redirect/{campaignSendLogId}")]
        //public async Task<IActionResult> TrackCampaignClick(
        //    Guid campaignSendLogId,
        //    [FromQuery] string type,
        //    [FromQuery] string to)
        //{
        //    if (string.IsNullOrWhiteSpace(to))
        //    {
        //        return BadRequest("Missing redirect target URL.");
        //    }

        //    var log = await _context.CampaignSendLogs.FindAsync(campaignSendLogId);
        //    if (log != null)
        //    {
        //        log.IsClicked = true;
        //        log.ClickedAt = DateTime.UtcNow;
        //        log.ClickType = type;
        //        log.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        //        await _context.SaveChangesAsync();
        //    }

        //    return Redirect(to);
        //}

        [HttpGet("redirect/{campaignSendLogId}")]
        public async Task<IActionResult> TrackCampaignClick(
    Guid campaignSendLogId,
    [FromQuery] string type,
    [FromQuery] string to,
    [FromQuery] int? idx = null,
    CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(to))
                return BadRequest("Missing redirect target URL.");

            if (!Uri.TryCreate(to, UriKind.Absolute, out var destUri))
                return BadRequest("Destination URL is invalid.");

            var clickType = string.IsNullOrWhiteSpace(type)
                ? ClassifyClickType(destUri)
                : type.Trim().ToLowerInvariant();

            // Load parent CSL (tracked so we can update it)
            var log = await _context.CampaignSendLogs.FindAsync(new object[] { campaignSendLogId }, ct);
            if (log is not null)
            {
                var now = DateTime.UtcNow;
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var ua = Request.Headers.UserAgent.ToString();

                // First-click update on the send (idempotent enough for now)
                log.IsClicked = true;
                log.ClickedAt = now;
                log.ClickType = clickType;
                log.IpAddress = ip;

                // Persist the click (ties to same run/session)
                await _context.CampaignClickLogs.AddAsync(new CampaignClickLog
                {
                    Id = Guid.NewGuid(),
                    CampaignSendLogId = log.Id,
                    CampaignId = log.CampaignId,// ?? Guid.Empty, // ✅ coalesce; make column Guid? to avoid this
                    ContactId = log.ContactId,                // Guid? OK
                    ButtonIndex = (short)(idx ?? 0),
                    ButtonTitle = string.IsNullOrWhiteSpace(type) ? "link" : type,
                    Destination = destUri.ToString(),
                    ClickedAt = now,
                    Ip = ip ?? "",
                    UserAgent = ua ?? "",
                    ClickType = clickType,
                    RunId = log.RunId                      // remove if your schema doesn’t have RunId
                }, ct);

                await _context.SaveChangesAsync(ct);
            }

            return Redirect(destUri.ToString());
        }

        // Simple classifier used above
        private static string ClassifyClickType(Uri u)
        {
            if (u == null) return "web";
            var scheme = u.Scheme?.ToLowerInvariant() ?? "";
            if (scheme == "tel") return "call";
            if (scheme == "whatsapp") return "whatsapp";
            if (scheme is "http" or "https")
            {
                var host = u.Host?.ToLowerInvariant() ?? "";
                if (host.Contains("wa.me") || host.Contains("api.whatsapp.com"))
                    return "whatsapp";
            }
            return "web";
        }

        /// <summary>
        /// Gets detailed information for a specific tracking log entry.
        /// </summary>
        [HttpGet("logs/{id}/details")]
        public async Task<IActionResult> GetLogDetails(Guid id)
        {
            var result = await _tracker.GetLogDetailsAsync(id);
            if (result == null)
                return NotFound("Tracking log not found");

            return Ok(result);
        }

        /// <summary>
        /// Retrieves click logs specifically related to automation flows.
        /// </summary>
        [HttpGet("flow-clicks")]
        public async Task<IActionResult> GetFlowClickLogs()
        {
            var businessIdClaim = User.FindFirst("businessId")?.Value;
            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return BadRequest("❌ Invalid or missing business ID");

            var logs = await _tracker.GetFlowClickLogsAsync(businessId);

            var dtoList = logs.Select(x => new
            {
                x.Id,
                x.StepId,
                x.ContactPhone,
                x.ButtonText,
                x.TemplateId,
                x.FollowUpSent,
                x.ClickedAt
            });

            return Ok(dtoList);
        }
        [HttpGet("journeys/{campaignSendLogId:guid}/export/csv")]
        public async Task<IActionResult> ExportJourneyCsv(Guid campaignSendLogId, CancellationToken ct)
        {
            var res = await _journeyExport.ExportJourneyCsvAsync(campaignSendLogId, ct);
            return File(res.Bytes, res.ContentType, res.FileName);
        }

        [HttpGet("journeys/{campaignSendLogId:guid}/export/xlsx")]
        public async Task<IActionResult> ExportJourneyXlsx(Guid campaignSendLogId, CancellationToken ct)
        {
            var res = await _journeyExport.ExportJourneyXlsxAsync(campaignSendLogId, ct);
            return File(res.Bytes, res.ContentType, res.FileName);
        }
    }
}
