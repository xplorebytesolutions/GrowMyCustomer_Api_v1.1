// 📄 xbytechat-api/Features/CRM/Summary/Controllers/CrmSummaryController.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CRM.Services;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CRM.Controllers
{
    /// <summary>
    /// Thin API surface for CRM summary endpoints used by Chat Inbox
    /// and future dashboards.
    /// </summary>
    [ApiController]
    [Route("api/crm-summary")]
    public sealed class CrmSummaryController : ControllerBase
    {
        private readonly IContactSummaryService _contactSummaryService;
        private readonly ILogger<CrmSummaryController> _logger;

        public CrmSummaryController(
            IContactSummaryService contactSummaryService,
            ILogger<CrmSummaryController> logger)
        {
            _contactSummaryService = contactSummaryService ?? throw new ArgumentNullException(nameof(contactSummaryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Returns a compact CRM snapshot for a given contact:
        /// Contact core fields, tags, recent notes, next reminder, recent timeline entries.
        /// </summary>
        [HttpGet("contact-summary/{contactId:guid}")]              // ✅ new route: /api/crm-summary/contact-summary/{id}
        [HttpGet("/api/crm/contact-summary/{contactId:guid}")]     // ✅ backward-compatible route: /api/crm/contact-summary/{id}
        public async Task<IActionResult> GetContactSummary(Guid contactId, CancellationToken ct)
        {
            var businessId = HttpContext.User.GetBusinessId();
            if (businessId == Guid.Empty)
            {
                return Unauthorized(ResponseResult.ErrorInfo("Missing BusinessId in user claims."));
            }

            try
            {
                var summary = await _contactSummaryService.GetContactSummaryAsync(businessId, contactId, ct);
                if (summary == null)
                {
                    return NotFound(ResponseResult.ErrorInfo("Contact not found for this business."));
                }

                return Ok(ResponseResult.SuccessInfo("Contact summary loaded.", summary));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "❌ Failed to load contact summary. Business={BusinessId}, Contact={ContactId}",
                    businessId,
                    contactId);

                return StatusCode(
                    500,
                    ResponseResult.ErrorInfo("An error occurred while loading contact summary.", ex.Message));
            }
        }
    }
}
