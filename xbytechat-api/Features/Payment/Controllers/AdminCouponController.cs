#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Payment.Enums;
using xbytechat.api.Features.Payment.Models;

namespace xbytechat.api.Features.Payment.Controllers
{
    /// <summary>
    /// Minimal admin-only APIs to manage coupons.
    /// Protect via role/policy in your auth setup.
    /// </summary>
    [ApiController]
    [Route("api/admin/payment/coupons")]
    [Authorize(Roles = "SuperAdmin")] // adjust to your actual role system
    public sealed class AdminCouponController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AdminCouponController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var items = await _db.Coupons
                .OrderByDescending(c => c.ValidFromUtc ?? DateTime.MinValue)
                .ToListAsync(ct);

            return Ok(new { ok = true, data = items });
        }

        public sealed class UpsertCouponRequest
        {
            public string Code { get; set; } = string.Empty;
            public string? Description { get; set; }
            public DiscountType DiscountType { get; set; }
            public decimal DiscountValue { get; set; }
            public DateTime? ValidFromUtc { get; set; }
            public DateTime? ValidToUtc { get; set; }
            public bool IsActive { get; set; } = true;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertCouponRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Code))
                return BadRequest(new { ok = false, message = "Code is required." });

            var exists = await _db.Coupons.AnyAsync(c => c.Code == req.Code, ct);
            if (exists)
                return Conflict(new { ok = false, message = "Coupon code already exists." });

            var coupon = new Coupon
            {
                Id = Guid.NewGuid(),
                Code = req.Code,
                Description = req.Description,
                DiscountType = req.DiscountType,
                DiscountValue = req.DiscountValue,
                ValidFromUtc = req.ValidFromUtc,
                ValidToUtc = req.ValidToUtc,
                IsActive = req.IsActive
            };

            _db.Coupons.Add(coupon);
            await _db.SaveChangesAsync(ct);

            return Ok(new { ok = true, data = coupon });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCouponRequest req, CancellationToken ct)
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (coupon is null)
                return NotFound(new { ok = false, message = "Coupon not found." });

            coupon.Description = req.Description;
            coupon.DiscountType = req.DiscountType;
            coupon.DiscountValue = req.DiscountValue;
            coupon.ValidFromUtc = req.ValidFromUtc;
            coupon.ValidToUtc = req.ValidToUtc;
            coupon.IsActive = req.IsActive;

            await _db.SaveChangesAsync(ct);

            return Ok(new { ok = true, data = coupon });
        }
    }
}
