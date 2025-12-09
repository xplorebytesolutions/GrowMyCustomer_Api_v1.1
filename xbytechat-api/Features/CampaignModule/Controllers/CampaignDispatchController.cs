using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/campaigns/{campaignId:guid}/dispatch")]
    [Authorize]
    public class CampaignDispatchController : ControllerBase
    {
        private readonly ICampaignDispatcher _dispatcher;

        public CampaignDispatchController(ICampaignDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Dispatch materialized recipients to the outbound queue.
        /// Query: mode=canary|full, count=25 (used when mode=canary).
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<CampaignDispatchResponseDto>> Dispatch(
            [FromRoute] Guid campaignId,
            [FromQuery] string mode = "canary",
            [FromQuery] int count = 25,
            CancellationToken ct = default)
        {
            try
            {
                var businessId = ResolveBusinessId();
                var resp = await _dispatcher.DispatchAsync(businessId, campaignId, mode, count, ct);
                return Ok(resp);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Dispatch failed for Campaign {CampaignId}", campaignId);
                return Problem(title: "Dispatch failed", detail: ex.Message, statusCode: 400);
            }
        }

        private Guid ResolveBusinessId()
        {
            var bid = User.GetBusinessId(); // unified helper
            if (bid == Guid.Empty)
                throw new UnauthorizedAccessException("Invalid or missing businessId claim.");
            return bid;
        }
    }
}
