using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.Audiences.Services;
using xbytechat.api.Shared; // User.GetBusinessId()

namespace xbytechat.api.Features.Audiences.Controllers
{
    [ApiController]
    [Route("api/audiences/import")]
    [Authorize]
    public class AudienceImportController : ControllerBase
    {
        private readonly IAudienceImportService _svc;

        public AudienceImportController(IAudienceImportService svc)
        {
            _svc = svc;
        }

        //[HttpPost("csv")]
        //[RequestSizeLimit(64_000_000)] // 64 MB
        //public async Task<IActionResult> ImportCsv([FromForm] IFormFile file)
        //{
        //    var businessId = User.GetBusinessId();
        //    if (businessId == Guid.Empty) return Unauthorized();

        //    if (file == null || file.Length == 0)
        //        return BadRequest(new { success = false, message = "CSV file is required" });

        //    try
        //    {
        //        await using var stream = file.OpenReadStream();
        //        var resp = await _svc.ImportCsvAsync(businessId, stream, file.FileName, HttpContext.RequestAborted);

        //        return Ok(new { success = true, data = resp });
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(ex, "❌ CSV import failed | biz={Biz}", businessId);
        //        return StatusCode(500, new { success = false, message = "CSV import failed" });
        //    }
        //}
    }
}
