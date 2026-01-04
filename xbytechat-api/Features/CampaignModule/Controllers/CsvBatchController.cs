using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Shared;   // User.GetBusinessId()
using xbytechat.api.Helpers;
using xbytechat.api.Features.CampaignModule.DTOs.Requests;  // ResponseResult

namespace xbytechat.api.Features.CampaignModule.Controllers
{
    [ApiController]
    [Route("api/csv/batch")]
    [Authorize]
    public class CsvBatchController : ControllerBase
    {
        private readonly ICsvBatchService _service;

        public CsvBatchController(ICsvBatchService service)
        {
            _service = service;
        }

        /// <summary>Upload a CSV, create a batch, and ingest rows.</summary>
        [HttpPost]
        [RequestSizeLimit(1024L * 1024L * 200L)] // 200 MB
        public async Task<IActionResult> Upload(
            [FromForm] CsvBatchUploadForm form,
            CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty)
                return Unauthorized(ResponseResult.ErrorInfo("Invalid business"));

            if (form.File is null || form.File.Length == 0)
                return BadRequest(ResponseResult.ErrorInfo("CSV file is required"));

            // soft sanity (many browsers use text/csv; don't block others)
            var allowed = new[] { "text/csv", "application/vnd.ms-excel", "application/octet-stream" };
            if (!allowed.Contains(form.File.ContentType, StringComparer.OrdinalIgnoreCase))
                Log.Warning("Unusual CSV content type: {ContentType}", form.File.ContentType);

            await using var stream = form.File.OpenReadStream();

            var result = await _service.CreateAndIngestAsync(
                businessId: businessId,
                fileName: form.File.FileName,
                stream: stream,
                audienceId: form.AudienceId,
                campaignId: form.CampaignId,
                ct: ct);

            return Ok(new { success = true, data = result });
        }


        [HttpGet("{batchId:guid}")]
        public async Task<IActionResult> Get(Guid batchId, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized(ResponseResult.ErrorInfo("Invalid business"));

            var result = await _service.GetBatchAsync(businessId, batchId, ct);
            if (result == null) return NotFound(ResponseResult.ErrorInfo("Batch not found"));
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{batchId:guid}/sample")]
        public async Task<IActionResult> Sample(Guid batchId, [FromQuery] int take = 20, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized(ResponseResult.ErrorInfo("Invalid business"));

            var rows = await _service.GetSamplesAsync(businessId, batchId, take, ct);
            return Ok(new { success = true, data = rows });
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int limit = 20, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized(ResponseResult.ErrorInfo("Invalid business"));

            var list = await _service.ListBatchesAsync(businessId, limit, ct);
            return Ok(new { success = true, data = list });
        }

        [HttpGet("{batchId:guid}/rows")]
        public async Task<IActionResult> RowsPage(Guid batchId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized(ResponseResult.ErrorInfo("Invalid business"));

            var page = await _service.GetRowsPageAsync(businessId, batchId, skip, take, ct);
            return Ok(new { success = true, data = page });
        }

        //[HttpPost("{batchId:guid}/validate")]
        //public async Task<IActionResult> Validate(Guid batchId, [FromBody] CsvBatchValidationRequestDto request, CancellationToken ct = default)
        //{
        //    var businessId = User.GetBusinessId();
        //    if (businessId == Guid.Empty) return Unauthorized(ResponseResult.ErrorInfo("Invalid business"));

        //    var result = await _service.ValidateAsync(businessId, batchId, request, ct);
        //    return Ok(new { success = true, data = result });
        //}

        [HttpDelete("{batchId:guid}")]
        public async Task<IActionResult> Delete(Guid batchId, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized(ResponseResult.ErrorInfo("Invalid business"));

            var ok = await _service.DeleteBatchAsync(businessId, batchId, ct);
            return ok ? Ok(new { success = true }) : NotFound(ResponseResult.ErrorInfo("Batch not found"));
        }
    }
}

