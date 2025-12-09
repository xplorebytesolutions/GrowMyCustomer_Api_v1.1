using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.Audiences.DTOs;
using xbytechat.api.Features.Audiences.Services;
using xbytechat.api.Shared; // User.GetBusinessId()

namespace xbytechat.api.Features.Audiences.Controllers
{
    [ApiController]
    [Route("api/audiences")]
    [Authorize]
    public class AudienceController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAudienceService _svc;

        public AudienceController(AppDbContext db, IAudienceService svc)
        { _db = db; _svc = svc; }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AudienceCreateDto dto)
        {
            var businessId = User.GetBusinessId();
            var userName = User.Identity?.Name ?? "system";
            if (businessId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto?.Name))
                return BadRequest(new { success = false, message = "Name is required" });

            var id = await _svc.CreateAsync(businessId, dto!, userName);
            return Ok(new { success = true, id });
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var items = await _svc.ListAsync(businessId);
            return Ok(new { success = true, items });
        }

        [HttpPost("{audienceId:guid}/assign")]
        public async Task<IActionResult> Assign(Guid audienceId, [FromBody] AudienceAssignDto dto)
        {
            var businessId = User.GetBusinessId();
            var userName = User.Identity?.Name ?? "system";
            if (businessId == Guid.Empty) return Unauthorized();

            var ok = await _svc.AssignAsync(businessId, audienceId, dto ?? new AudienceAssignDto(), userName);
            return Ok(new { success = ok });
        }

        [HttpGet("{audienceId:guid}/members")]
        public async Task<IActionResult> Members(Guid audienceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var exists = await _db.Audiences.AnyAsync(a => a.Id == audienceId && a.BusinessId == businessId && !a.IsDeleted);
            if (!exists) return NotFound(new { success = false, message = "Audience not found" });

            var rows = await _svc.GetMembersAsync(businessId, audienceId, page, pageSize);
            return Ok(new { success = true, items = rows, page, pageSize });
        }
    }
}
