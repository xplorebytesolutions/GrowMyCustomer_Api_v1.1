using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.Inbox.Services;
using xbytechat.api.Helpers; // for User.GetBusinessId(), GetUserId()
using xbytechat.api.Shared;

namespace xbytechat.api.Features.Inbox.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/quick-replies")]
    public class QuickRepliesController : ControllerBase
    {
        private readonly IQuickReplyService _service;

        public QuickRepliesController(IQuickReplyService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? q = null,
            [FromQuery] string scope = "all")
        {
            var businessId = User.GetBusinessId();
            var userId = User.GetUserId();

            // Guid is a value type, so null-checks are useless.
            // Treat Guid.Empty as "no id in claims".
            if (businessId == Guid.Empty || userId == Guid.Empty)
                return Unauthorized();

            bool includeBusiness = scope is "all" or "business";
            bool includePersonal = scope is "all" or "personal";

            var list = await _service.GetAllAsync(businessId, userId, q, includeBusiness, includePersonal);
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] QuickReplyCreateDto dto)
        {
            var businessId = User.GetBusinessId();
            var userId = User.GetUserId();

            if (businessId == Guid.Empty || userId == Guid.Empty)
                return Unauthorized();

            var actor = User.Identity?.Name
                        ?? (userId != Guid.Empty ? userId.ToString() : "system");

            Log.Information("Create QuickReply requested by {@Actor}", actor);
            var result = await _service.CreateAsync(businessId, userId, actor, dto);
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] QuickReplyUpdateDto dto)
        {
            var businessId = User.GetBusinessId();
            var userId = User.GetUserId();

            if (businessId == Guid.Empty || userId == Guid.Empty)
                return Unauthorized();

            var actor = User.Identity?.Name
                        ?? (userId != Guid.Empty ? userId.ToString() : "system");

            Log.Information("Update QuickReply {@QuickReplyId} by {@Actor}", id, actor);
            var result = await _service.UpdateAsync(businessId, userId, actor, id, dto);
            return Ok(result);
        }

        [HttpPatch("{id:guid}/toggle")]
        public async Task<IActionResult> Toggle([FromRoute] Guid id, [FromQuery] bool active = true)
        {
            var businessId = User.GetBusinessId();
            var userId = User.GetUserId();

            if (businessId == Guid.Empty || userId == Guid.Empty)
                return Unauthorized();

            var actor = User.Identity?.Name
                        ?? (userId != Guid.Empty ? userId.ToString() : "system");

            Log.Information("Toggle QuickReply {@QuickReplyId} -> {Active} by {@Actor}", id, active, actor);
            var result = await _service.ToggleActiveAsync(businessId, userId, actor, id, active);
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var businessId = User.GetBusinessId();
            var userId = User.GetUserId();

            if (businessId == Guid.Empty || userId == Guid.Empty)
                return Unauthorized();

            var actor = User.Identity?.Name
                        ?? (userId != Guid.Empty ? userId.ToString() : "system");

            Log.Information("Delete QuickReply {@QuickReplyId} by {@Actor}", id, actor);
            var result = await _service.DeleteAsync(businessId, userId, actor, id);
            return Ok(result);
        }
    }
}


//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Serilog;
//using xbytechat.api.Features.Inbox.DTOs;
//using xbytechat.api.Features.Inbox.Services;
//using xbytechat.api.Helpers; // for User.GetBusinessId(), GetUserId()
//using xbytechat.api.Shared;

//namespace xbytechat.api.Features.Inbox.Controllers
//{
//    [Authorize]
//    [ApiController]
//    [Route("api/quick-replies")]
//    public class QuickRepliesController : ControllerBase
//    {
//        private readonly IQuickReplyService _service;

//        public QuickRepliesController(IQuickReplyService service) => _service = service;

//        [HttpGet]
//        public async Task<IActionResult> GetAll([FromQuery] string? q = null,
//            [FromQuery] string scope = "all")
//        {
//            var businessId = User.GetBusinessId();
//            var userId = User.GetUserId();

//            if (businessId == null || userId == null) return Unauthorized();

//            bool includeBusiness = scope is "all" or "business";
//            bool includePersonal = scope is "all" or "personal";

//            var list = await _service.GetAllAsync(businessId, userId, q, includeBusiness, includePersonal);
//            return Ok(list);
//        }

//        [HttpPost]
//        public async Task<IActionResult> Create([FromBody] QuickReplyCreateDto dto)
//        {
//            var businessId = User.GetBusinessId();
//            var userId = User.GetUserId();
//            var actor = User.Identity?.Name ?? userId.ToString() ?? "system";

//            if (businessId == null || userId == null) return Unauthorized();

//            Log.Information("Create QuickReply requested by {@Actor}", actor);
//            var result = await _service.CreateAsync(businessId, userId, actor, dto);
//            return Ok(result);
//        }

//        [HttpPut("{id:guid}")]
//        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] QuickReplyUpdateDto dto)
//        {
//            var businessId = User.GetBusinessId();
//            var userId = User.GetUserId();
//            var actor = User.Identity?.Name ?? userId.ToString() ?? "system";

//            if (businessId == null || userId == null) return Unauthorized();

//            Log.Information("Update QuickReply {@QuickReplyId} by {@Actor}", id, actor);
//            var result = await _service.UpdateAsync(businessId, userId, actor, id, dto);
//            return Ok(result);
//        }

//        [HttpPatch("{id:guid}/toggle")]
//        public async Task<IActionResult> Toggle([FromRoute] Guid id, [FromQuery] bool active = true)
//        {
//            var businessId = User.GetBusinessId();
//            var userId = User.GetUserId();
//            var actor = User.Identity?.Name ?? userId.ToString() ?? "system";

//            if (businessId == null || userId == null) return Unauthorized();

//            Log.Information("Toggle QuickReply {@QuickReplyId} -> {Active} by {@Actor}", id, active, actor);
//            var result = await _service.ToggleActiveAsync(businessId, userId, actor, id, active);
//            return Ok(result);
//        }

//        [HttpDelete("{id:guid}")]
//        public async Task<IActionResult> Delete([FromRoute] Guid id)
//        {
//            var businessId = User.GetBusinessId();
//            var userId = User.GetUserId();
//            var actor = User.Identity?.Name ?? userId.ToString() ?? "system";

//            if (businessId == null || userId == null) return Unauthorized();

//            Log.Information("Delete QuickReply {@QuickReplyId} by {@Actor}", id, actor);
//            var result = await _service.DeleteAsync(businessId, userId, actor, id);
//            return Ok(result);
//        }
//    }
//}
