using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.AccountInsights.DTOs;
using xbytechat.api.Features.AccountInsights.Models;
using xbytechat.api.Features.AccountInsights.Services;
using xbytechat.api.Features.PlanManagement.Models;
using System.Text.Json;
namespace xbytechat.api.Features.AccountInsights.Controllers
{
    [ApiController]
    [Route("api/admin/account-insights")]
    [Authorize(Roles = "admin,superadmin,partner")]
    public class AccountInsightsController : ControllerBase
    {
        private readonly IAccountInsightsService _svc;
        private readonly AppDbContext _db;
        public AccountInsightsController(IAccountInsightsService svc, AppDbContext db)
        {
            _svc = svc;
            _db = db;
        }

        [HttpGet("{businessId:guid}")]
        public async Task<IActionResult> GetOne(Guid businessId)
        {
            var snapshot = await _svc.GetSnapshotAsync(businessId);
            return Ok(snapshot);
        }

        [HttpGet]
        public async Task<IActionResult> GetMany([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] Guid? partnerId = null)
        {
            var snapshots = await _svc.GetSnapshotsAsync(page, pageSize, partnerId);
            return Ok(snapshots);
        }
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] Guid? partnerId = null)
        {
            var summary = await _svc.GetSummaryAsync(partnerId);
            return Ok(summary);
        }

        [HttpGet("trial-expiring-soon")]
        public async Task<IActionResult> GetTrialsExpiringSoon([FromQuery] int days = 3)
        {
            var items = await _svc.GetTrialsExpiringSoonAsync(days);
            return Ok(items);
        }
        [HttpGet("by-stage")]
        public async Task<IActionResult> GetByStage(
            [FromQuery] AccountLifecycleStage stage,
            [FromQuery] Guid? partnerId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            var items = await _svc.GetByLifecycleStageAsync(stage, partnerId, page, pageSize);
            return Ok(items);
        }
        [HttpGet("segment")]
        public async Task<IActionResult> GetSegment(
            [FromQuery] string key,
            [FromQuery] Guid? partnerId = null,
            [FromQuery] int days = 3,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200)
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest("Segment key is required.");

            key = key.Trim().ToLowerInvariant();

            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 500) pageSize = 200;

            IReadOnlyList<AccountInsightsSnapshotDto> items;

            switch (key)
            {
                case "trials-near-expiry":
                    // Trials whose trial window ends within next {days}
                    items = await _svc.GetTrialsExpiringSoonAsync(days);
                    break;

                case "no-usage-post-approval":
                case "wa-setup-no-usage":
                    // Approved, never sent a message
                    items = await _svc.GetByLifecycleStageAsync(
                        AccountLifecycleStage.NoUsagePostApproval,
                        partnerId,
                        page,
                        pageSize);
                    break;

                case "at-risk":
                    items = await _svc.GetByLifecycleStageAsync(
                        AccountLifecycleStage.AtRisk,
                        partnerId,
                        page,
                        pageSize);
                    break;

                case "dormant":
                    items = await _svc.GetByLifecycleStageAsync(
                        AccountLifecycleStage.Dormant,
                        partnerId,
                        page,
                        pageSize);
                    break;

                case "healthy-active":
                    items = await _svc.GetByLifecycleStageAsync(
                        AccountLifecycleStage.Active,
                        partnerId,
                        page,
                        pageSize);
                    break;

                default:
                    return BadRequest($"Unknown segment key '{key}'.");
            }

            return Ok(items);
        }

        [HttpGet("{businessId:guid}/actions")]
        public async Task<IActionResult> GetRecentActions(Guid businessId, [FromQuery] int limit = 10)
        {
            var items = await _svc.GetRecentActionsAsync(businessId, limit);
            // Frontend supports both array and { items }; use { items } for clarity.
            return Ok(new { items });
        }
        private string GetActor()
        {
            // Prefer email/username, fall back to subject or role
            var email = User?.FindFirst(ClaimTypes.Email)?.Value;
            var name = User?.Identity?.Name;

            return !string.IsNullOrWhiteSpace(email)
                ? email
                : !string.IsNullOrWhiteSpace(name)
                    ? name
                    : "admin";
        }

        [HttpPost("{businessId:guid}/mark-contacted")]
        public async Task<IActionResult> MarkContacted(Guid businessId)
        {
            // No schema flag change; purely timeline-based
            var actor = GetActor();

            await _svc.LogActionAsync(
                businessId,
                AccountInsightActionTypes.TagContacted,
                "Marked as contacted",
                actor);

            return Ok(new
            {
                ok = true,
                businessId,
                contacted = true
            });
        }
        public class ExtendTrialRequest
        {
            public int ExtraDays { get; set; }
        }

        [HttpPost("{businessId:guid}/extend-trial")]
        public async Task<IActionResult> ExtendTrial(
            Guid businessId,
            [FromBody] ExtendTrialRequest body)
        {
            if (body == null || body.ExtraDays <= 0 || body.ExtraDays > 365)
                return BadRequest("ExtraDays must be between 1 and 365.");

            var actor = GetActor();

            var biz = await _db.Businesses
                .Include(b => b.BusinessPlanInfo)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (biz == null)
                return NotFound("Business not found.");

            var plan = biz.BusinessPlanInfo;
            if (plan == null)
                return BadRequest("Business has no plan info.");

            if (plan.Plan != PlanType.Trial)
                return BadRequest("Trial extension is only allowed for Trial plan.");

            var now = DateTime.UtcNow;

            // current end date resolution (aligned with BuildSnapshotAsync)
            var trialStart = plan.CreatedAt != default
                ? plan.CreatedAt
                : biz.CreatedAt;

            DateTime currentEnd;
            if (plan.QuotaResetDate != default && plan.QuotaResetDate > trialStart)
            {
                currentEnd = plan.QuotaResetDate;
            }
            else
            {
                currentEnd = trialStart.AddDays(14); // fallback consistent with DefaultTrialDaysFallback
            }

            var oldEnd = currentEnd;
            var newEnd = oldEnd.AddDays(body.ExtraDays);

            plan.QuotaResetDate = newEnd;
            await _db.SaveChangesAsync();

            var meta = JsonSerializer.Serialize(new
            {
                oldEnd,
                newEnd,
                extraDays = body.ExtraDays
            });

            await _svc.LogActionAsync(
                businessId,
                AccountInsightActionTypes.ExtendTrial,
                $"Trial extended by {body.ExtraDays} days",
                actor,
                meta);

            // Return updated snapshot so frontend stays in sync without extra roundtrip
            var snapshot = await _svc.GetSnapshotAsync(businessId);

            return Ok(new
            {
                ok = true,
                businessId,
                oldEnd,
                newEnd,
                snapshot
            });
        }


    }
}
