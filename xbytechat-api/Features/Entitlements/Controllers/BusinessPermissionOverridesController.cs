using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Entitlements.DTOs;
using xbytechat.api.Features.Entitlements.Services;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Entitlements.Controllers
{
    [ApiController]
    [Route("api/admin/businesses/{businessId:guid}/permission-overrides")]
    [Authorize(Roles = "admin,partner,reseller")]
    public sealed class BusinessPermissionOverridesController : ControllerBase
    {
        private readonly IBusinessPermissionOverrideService _service;

        public BusinessPermissionOverridesController(IBusinessPermissionOverrideService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(Guid businessId)
        {
            var rows = await _service.GetAsync(businessId);
            return Ok(ResponseResult.SuccessInfo("✅ Overrides fetched.", rows));
        }

        [HttpPost]
        public async Task<IActionResult> Upsert(Guid businessId, [FromBody] UpsertBusinessPermissionOverrideDto dto)
        {
            var actorUserId = GetUserId();
            var res = await _service.UpsertAsync(businessId, actorUserId, dto);
            return Ok(res);
        }

        [HttpDelete("{permissionCode}")]
        public async Task<IActionResult> Revoke(Guid businessId, string permissionCode)
        {
            var actorUserId = GetUserId();
            var res = await _service.RevokeByPermissionCodeAsync(businessId, actorUserId, permissionCode);
            return Ok(res);
        }

        private Guid GetUserId()
        {
            var raw = User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}
