// 📄 Features/Entitlements/Controllers/PlanQuotasAdminController.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Entitlements.DTOs;
using xbytechat.api.Features.Entitlements.Models;

namespace xbytechat.api.Features.Entitlements.Controllers
{
    /// <summary>
    /// Admin endpoints to manage default quotas per plan.
    /// These are the rows in PlanQuotas table.
    /// </summary>
    [ApiController]
    [Route("api/admin/plans/{planId:guid}/quotas")]
    [Authorize(Roles = "superadmin,partneradmin,admin")]
    public sealed class PlanQuotasAdminController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PlanQuotasAdminController(AppDbContext db)
        {
            _db = db;
        }

        // GET /admin/plans/{planId}/quotas
        [HttpGet]
        public async Task<ActionResult<List<PlanQuotaDto>>> GetForPlan(
            Guid planId,
            CancellationToken ct)
        {
            // Validate plan exists (defensive)
            var planExists = await _db.Plans
                .AsNoTracking()
                .AnyAsync(p => p.Id == planId, ct);

            if (!planExists)
                return NotFound(new { message = "Plan not found" });

            var quotas = await _db.PlanQuotas
                .AsNoTracking()
                .Where(q => q.PlanId == planId)
                .OrderBy(q => q.QuotaKey)
                .Select(q => new PlanQuotaDto
                {
                    Id = q.Id,
                    PlanId = q.PlanId,
                    QuotaKey = q.QuotaKey,
                    Limit = q.Limit,
                    Period = q.Period,
                    DenialMessage = q.DenialMessage
                })
                .ToListAsync(ct);

            return Ok(quotas);
        }

        // PUT /admin/plans/{planId}/quotas
        //
        // Simple "upsert by QuotaKey" semantics:
        // - Existing PlanQuota with same PlanId + QuotaKey is updated
        // - New QuotaKey rows are inserted
        // - Quotas not present in payload are kept (no destructive delete here)
        [HttpPut]
        public async Task<IActionResult> UpsertForPlan(
            Guid planId,
            [FromBody] List<PlanQuotaDto> payload,
            CancellationToken ct)
        {
            if (payload is null)
                return BadRequest(new { message = "Payload is required" });

            // Normalize keys to upper-case for comparisons (used only in-memory)
            static string Normalize(string key) =>
                (key ?? string.Empty).Trim().ToUpperInvariant();

            // Ensure plan exists
            var planExists = await _db.Plans
                .AsNoTracking()
                .AnyAsync(p => p.Id == planId, ct);

            if (!planExists)
                return NotFound(new { message = "Plan not found" });

            var incoming = payload
                .Where(p => !string.IsNullOrWhiteSpace(p.QuotaKey))
                .Select(p => new
                {
                    Raw = p,
                    NormalizedKey = Normalize(p.QuotaKey!)
                })
                .ToList();

            if (!incoming.Any())
                return BadRequest(new { message = "At least one quota with a QuotaKey is required." });

            var keys = incoming
                .Select(i => i.NormalizedKey)
                .Distinct()
                .ToList();

            // ✅ IMPORTANT: bring data into memory first, then call Normalize
            var existingAllForPlan = await _db.PlanQuotas
                .Where(q => q.PlanId == planId)
                .ToListAsync(ct);

            // optional: only keep rows whose normalized key is in payload keys
            var existing = existingAllForPlan
                .Where(q => keys.Contains(Normalize(q.QuotaKey)))
                .ToList();
            // 👉 NEW: delete quotas that are no longer present in the payload
            var toDelete = existingAllForPlan
                .Where(q => !keys.Contains(Normalize(q.QuotaKey)))
                .ToList();

            if (toDelete.Count > 0)
            {
                _db.PlanQuotas.RemoveRange(toDelete);
            }

            foreach (var item in incoming)
            {
                var dto = item.Raw;
                var normalizedKey = item.NormalizedKey;

                var entity = existing
                    .FirstOrDefault(q => Normalize(q.QuotaKey) == normalizedKey);

                if (entity is null)
                {
                    // Insert new row
                    entity = new PlanQuota
                    {
                        Id = Guid.NewGuid(),
                        PlanId = planId,
                        QuotaKey = normalizedKey,
                        Limit = dto.Limit,
                        Period = dto.Period,
                        DenialMessage = dto.DenialMessage,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _db.PlanQuotas.Add(entity);
                    existing.Add(entity); // keep in local list too
                }
                else
                {
                    // Update existing row
                    entity.QuotaKey = normalizedKey;
                    entity.Limit = dto.Limit;
                    entity.Period = dto.Period;
                    entity.DenialMessage = dto.DenialMessage;
                    entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
