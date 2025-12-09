using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CTAManagement.DTOs;
using xbytechat.api.Features.CTAManagement.Services;

namespace xbytechat.api.Features.CTAManagement.Controllers
{
    [ApiController]
    [Route("api/ctamanagement")]
    [Authorize] // ✅ Ensures only authenticated users can access
    public class CTAManagementController : ControllerBase
    {
        private readonly ICTAManagementService _ctaService;

        public CTAManagementController(ICTAManagementService ctaService)
        {
            _ctaService = ctaService;
        }

        // ✅ GET: api/ctamanagement/get-all
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll()
        {
            var data = await _ctaService.GetAllAsync();
            return Ok(data);
        }

        // 📌 GET: api/ctamanagement/get/{id}
        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _ctaService.GetByIdAsync(id);
            return result == null ? NotFound("CTA not found") : Ok(result);
        }

        // ✅ POST: api/ctamanagement/create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CTADefinitionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("❌ Invalid CTA payload.");

            var success = await _ctaService.AddAsync(dto);
            return success
                ? Ok(new { message = "✅ CTA created." })
                : StatusCode(500, "❌ Failed to create CTA.");
        }

        // ✏️ PUT: api/ctamanagement/update/{id}
        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CTADefinitionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("❌ Invalid CTA payload.");

            var success = await _ctaService.UpdateAsync(id, dto);
            return success
                ? Ok(new { message = "✅ CTA updated." })
                : NotFound("CTA not found or update failed.");
        }

        // 🗑️ DELETE: api/ctamanagement/delete/{id}
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _ctaService.DeleteAsync(id);
            return success
                ? Ok(new { message = "✅ CTA deleted (soft)." })
                : NotFound("CTA not found or delete failed.");
        }
    }
}
