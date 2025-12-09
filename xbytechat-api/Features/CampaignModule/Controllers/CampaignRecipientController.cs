using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CampaignRecipientController : ControllerBase
    {
        private readonly ICampaignRecipientService _recipientService;

        public CampaignRecipientController(ICampaignRecipientService recipientService)
        {
            _recipientService = recipientService;
        }

        // ✅ Get a single recipient by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<CampaignRecipientDto>> GetRecipientById(Guid id)
        {
            var recipient = await _recipientService.GetByIdAsync(id);
            if (recipient == null)
                return NotFound(new { message = "Recipient not found" });

            return Ok(recipient);
        }

        // ✅ Get all recipients for a specific campaign
        [HttpGet("/api/campaigns/{campaignId}/recipients")]
        public async Task<ActionResult> GetRecipientsForCampaign(Guid campaignId)
        {
            var recipients = await _recipientService.GetByCampaignIdAsync(campaignId);
            return Ok(recipients);
        }

        // ✅ Update recipient status (e.g., from Pending → Sent)
        [HttpPut("{recipientId}/status")]
        public async Task<ActionResult> UpdateStatus(Guid recipientId, [FromQuery] string newStatus)
        {
            var success = await _recipientService.UpdateStatusAsync(recipientId, newStatus);
            if (!success)
                return NotFound(new { message = "Recipient not found or update failed" });

            return Ok(new { message = "Status updated" });
        }

        // ✅ Track a reply from customer
        [HttpPut("{recipientId}/reply")]
        public async Task<ActionResult> TrackReply(Guid recipientId, [FromQuery] string replyText)
        {
            var success = await _recipientService.TrackReplyAsync(recipientId, replyText);
            if (!success)
                return NotFound(new { message = "Recipient not found or tracking failed" });

            return Ok(new { message = "Reply tracked" });
        }

        // 🔍 Search recipients by optional filters (status, keyword)
        [HttpGet("search")]
        public async Task<ActionResult<List<CampaignRecipientDto>>> SearchRecipients([FromQuery] string? status, [FromQuery] string? keyword)
        {
            var results = await _recipientService.SearchRecipientsAsync(status, keyword);
            return Ok(results);
        }

        [HttpPost("{id}/assign-contacts")]
        public async Task<IActionResult> AssignContacts(Guid id, [FromBody] AssignContactsDto dto)
        {
            try
            {
                await _recipientService.AssignContactsToCampaignAsync(id, dto.ContactIds);
                return Ok(new { message = "Contacts assigned successfully" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error assigning contacts to campaign");
                return StatusCode(500, new { message = "Failed to assign contacts" });
            }
        }
    }
}
