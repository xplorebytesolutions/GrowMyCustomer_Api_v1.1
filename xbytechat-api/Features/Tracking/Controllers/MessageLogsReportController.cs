using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.Shared; // for User.GetBusinessId()

namespace xbytechat.api.Features.Tracking.Controllers
{
    [ApiController]
    [Route("api/report/message-logs")]
    [Authorize]
    public sealed class MessageLogsReportController : ControllerBase
    {
        private readonly IMessageLogsReportService _service;

        public MessageLogsReportController(IMessageLogsReportService service)
            => _service = service;

        [HttpPost("search")]
        [ProducesResponseType(typeof(PaginatedResponse<MessageLogListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Search([FromBody] MessageLogReportQueryDto q, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var result = await _service.SearchAsync(businessId, q, ct);
            return Ok(result);
        }

        [HttpPost("export/csv")]
        [Produces("text/csv")]
        public async Task<IActionResult> ExportCsv([FromBody] MessageLogReportQueryDto q, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            // We’ll walk pages to get the full dataset (service clamps PageSize to <= 200).
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",", new[]
            {
                "Time","Recipient","SenderId","Channel","Status","Type","Campaign",
                "Body","ProviderId","DeliveredAt","ReadAt","Error"
            }.Select(EscapeCsv)));

            var page = 1;
            const int maxLoops = 10000; // safety
            var totalWritten = 0;

            while (page <= maxLoops)
            {
                var pageQuery = new MessageLogReportQueryDto
                {
                    FromUtc = q.FromUtc,
                    ToUtc = q.ToUtc,
                    Search = q.Search,
                    Statuses = q.Statuses,
                    Channels = q.Channels,
                    SenderIds = q.SenderIds,
                    MessageTypes = q.MessageTypes,
                    CampaignId = q.CampaignId,
                    SortBy = q.SortBy,
                    SortDir = q.SortDir,
                    Page = page,
                    PageSize = 200 // max page supported by service
                };

                var res = await _service.SearchAsync(businessId, pageQuery, ct);
                if (res.Items.Count == 0) break;

                foreach (var r in res.Items)
                {
                    var time = r.SentAt ?? r.CreatedAt;
                    var row = new[]
                    {
                        EscapeCsv(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                        EscapeCsv(r.RecipientNumber ?? ""),
                        EscapeCsv(r.SenderId ?? ""),
                        EscapeCsv(r.SourceChannel ?? ""),
                        EscapeCsv(r.Status ?? ""),
                        EscapeCsv(r.MessageType ?? ""),
                        EscapeCsv(r.CampaignName ?? r.CampaignId?.ToString() ?? ""),
                        EscapeCsv((r.MessageContent ?? "").ReplaceLineEndings(" ")),
                        EscapeCsv(r.ProviderMessageId ?? ""),
                        EscapeCsv(r.DeliveredAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                        EscapeCsv(r.ReadAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                        EscapeCsv((r.ErrorMessage ?? "").ReplaceLineEndings(" "))
                    };
                    sb.AppendLine(string.Join(",", row));
                }

                totalWritten += res.Items.Count;

                // stop when we’ve written everything
                var total = res.TotalCount;
                if (totalWritten >= total) break;

                page++;
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName =
                $"MessageLogs{(q.CampaignId.HasValue ? "-" + q.CampaignId.Value : "")}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            return File(bytes, "text/csv", fileName);

            static string EscapeCsv(string s)
            {
                // Quote if contains comma, quote, or newline
                if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                {
                    return $"\"{s.Replace("\"", "\"\"")}\"";
                }
                return s;
            }
        }

     
        [HttpPost("export/pdf")]
        public IActionResult ExportPdfStub()
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
            {
                Title = "PDF export is not enabled",
                Detail = "Implement in MessageLogsReportController.ExportPdf using a PDF library."
            });
        }

        [HttpPost("export/xlsx")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        public async Task<IActionResult> ExportXlsx([FromBody] MessageLogReportQueryDto q, CancellationToken ct)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            // Page through all results using the same service used by the grid.
            const int pageSize = 200; // service already clamps; keep explicit for clarity
            var all = new List<MessageLogListItemDto>(capacity: pageSize * 5); // small pre-alloc

            var page = 1;
            while (true)
            {
                var pageQuery = new MessageLogReportQueryDto
                {
                    FromUtc = q.FromUtc,
                    ToUtc = q.ToUtc,
                    Search = q.Search,
                    Statuses = q.Statuses,
                    Channels = q.Channels,
                    SenderIds = q.SenderIds,
                    MessageTypes = q.MessageTypes,
                    CampaignId = q.CampaignId,
                    SortBy = q.SortBy,
                    SortDir = q.SortDir,
                    Page = page,
                    PageSize = pageSize
                };

                var res = await _service.SearchAsync(businessId, pageQuery, ct);
                if (res.Items.Count == 0) break;

                all.AddRange(res.Items);

                if (all.Count >= res.TotalCount) break; // done
                page++;
            }

            // Build the workbook in memory
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("MessageLogs");

            // Header
            var headers = new[]
            {
        "Time","Recipient","SenderId","Channel","Status","Type","Campaign",
        "Body","ProviderId","DeliveredAt","ReadAt","Error"
    };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            // Simple header style
            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6"); // light gray
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Rows
            int r = 2;
            foreach (var it in all)
            {
                var time = (it.SentAt ?? it.CreatedAt).ToLocalTime();

                ws.Cell(r, 1).Value = time;
                ws.Cell(r, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";

                ws.Cell(r, 2).Value = it.RecipientNumber ?? "";
                ws.Cell(r, 3).Value = it.SenderId ?? "";
                ws.Cell(r, 4).Value = it.SourceChannel ?? "";
                ws.Cell(r, 5).Value = it.Status ?? "";
                ws.Cell(r, 6).Value = it.MessageType ?? "";
                ws.Cell(r, 7).Value = it.CampaignName ?? (it.CampaignId?.ToString() ?? "");

                // Body/Errors as plain text to avoid newlines breaking rows
                ws.Cell(r, 8).Value = (it.MessageContent ?? "").ReplaceLineEndings(" ");
                ws.Cell(r, 9).Value = it.ProviderMessageId ?? "";

                if (it.DeliveredAt.HasValue)
                {
                    ws.Cell(r, 10).Value = it.DeliveredAt.Value.ToLocalTime();
                    ws.Cell(r, 10).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                }
                else ws.Cell(r, 10).Value = "";

                if (it.ReadAt.HasValue)
                {
                    ws.Cell(r, 11).Value = it.ReadAt.Value.ToLocalTime();
                    ws.Cell(r, 11).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                }
                else ws.Cell(r, 11).Value = "";

                ws.Cell(r, 12).Value = (it.ErrorMessage ?? "").ReplaceLineEndings(" ");
                r++;
            }

            // Fit columns
            ws.Columns().AdjustToContents();

            // Stream to client
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            var fileName = $"MessageLogs-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(ms.ToArray(), contentType, fileName);
        }
        [HttpGet("facets")]
        [ProducesResponseType(typeof(MessageLogFacetsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Facets([FromQuery] int fromDays = 90, CancellationToken ct = default)
        {
            var businessId = User.GetBusinessId();
            if (businessId == Guid.Empty) return Unauthorized();

            var fromUtc = DateTime.UtcNow.AddDays(-Math.Abs(fromDays));
            var facets = await _service.GetFacetsAsync(businessId, fromUtc, ct);
            return Ok(facets);
        }
    }
}
