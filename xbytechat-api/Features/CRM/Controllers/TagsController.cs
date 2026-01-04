using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TagsController : ControllerBase
    {
        private readonly ITagService _tagService;

        public TagsController(ITagService tagService)
        {
            _tagService = tagService;
        }

        [HttpPost]
        public async Task<IActionResult> AddTag([FromBody] TagDto dto)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var result = await _tagService.AddTagAsync(businessId, dto);
            return Ok(ResponseResult.SuccessInfo("Tag created.", result));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTag(Guid id, [FromBody] TagDto dto)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _tagService.UpdateTagAsync(businessId, id, dto);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Tag not found."));
            return Ok(ResponseResult.SuccessInfo("Tag updated."));
        }

        ////[HttpGet("get-tags")]
        [HttpGet("get-tags")]
        public async Task<IActionResult> GetAllTagsLegacy()
        {
            // Backward compatible alias for older clients
            var businessId = HttpContext.User.GetBusinessId();
            var tags = await _tagService.GetAllTagsAsync(businessId);
            return Ok(ResponseResult.SuccessInfo("Tags loaded.", tags));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTags()
        {
            var businessId = HttpContext.User.GetBusinessId();
            var tags = await _tagService.GetAllTagsAsync(businessId);
            return Ok(ResponseResult.SuccessInfo("Tags loaded.", tags));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _tagService.DeleteTagAsync(businessId, id);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Tag not found."));
            return Ok(ResponseResult.SuccessInfo("Tag deleted."));
        }
    }
}
