using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared; // User.GetBusinessId()

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CampaignRecipientController : ControllerBase
    {
        private readonly ICampaignRecipientService _recipientService;

        public CampaignRecipientController(ICampaignRecipientService recipientService)
        {
            _recipientService = recipientService;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CampaignRecipientDto>> GetRecipientById(Guid id)
        {
            Guid businessId;
            try { businessId = User.GetBusinessId(); }
            catch { return Unauthorized(); }

            var recipient = await _recipientService.GetByIdAsync(businessId, id);
            if (recipient == null)
                return NotFound(new { message = "Recipient not found" });

            return Ok(recipient);
        }

        [HttpGet("/api/campaigns/{campaignId}/recipients")]
        public async Task<ActionResult> GetRecipientsForCampaign(Guid campaignId)
        {
            Guid businessId;
            try { businessId = User.GetBusinessId(); }
            catch { return Unauthorized(); }

            var recipients = await _recipientService.GetByCampaignIdAsync(businessId, campaignId);
            return Ok(recipients);
        }

        [HttpPut("{recipientId}/status")]
        public async Task<ActionResult> UpdateStatus(Guid recipientId, [FromQuery] string newStatus)
        {
            Guid businessId;
            try { businessId = User.GetBusinessId(); }
            catch { return Unauthorized(); }

            var success = await _recipientService.UpdateStatusAsync(businessId, recipientId, newStatus);
            if (!success)
                return NotFound(new { message = "Recipient not found or update failed" });

            return Ok(new { message = "Status updated" });
        }

        [HttpPut("{recipientId}/reply")]
        public async Task<ActionResult> TrackReply(Guid recipientId, [FromQuery] string replyText)
        {
            Guid businessId;
            try { businessId = User.GetBusinessId(); }
            catch { return Unauthorized(); }

            var success = await _recipientService.TrackReplyAsync(businessId, recipientId, replyText);
            if (!success)
                return NotFound(new { message = "Recipient not found or tracking failed" });

            return Ok(new { message = "Reply tracked" });
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<CampaignRecipientDto>>> SearchRecipients([FromQuery] string? status, [FromQuery] string? keyword)
        {
            Guid businessId;
            try { businessId = User.GetBusinessId(); }
            catch { return Unauthorized(); }

            var results = await _recipientService.SearchRecipientsAsync(businessId, status, keyword);
            return Ok(results);
        }

        [HttpPost("{id}/assign-contacts")]
        public async Task<IActionResult> AssignContacts(Guid id, [FromBody] AssignContactsDto dto)
        {
            try
            {
                Guid businessId;
                try { businessId = User.GetBusinessId(); }
                catch { return Unauthorized(); }

                await _recipientService.AssignContactsToCampaignAsync(businessId, id, dto.ContactIds);
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
