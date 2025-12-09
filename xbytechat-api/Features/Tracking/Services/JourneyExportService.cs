using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using xbytechat.api.Features.Tracking.Services;

namespace xbytechat.api.Features.Tracking.Services
{
    public sealed class JourneyExportService : IJourneyExportService
    {
        private readonly IContactJourneyService _journeyService;

        public JourneyExportService(IContactJourneyService journeyService)
        {
            _journeyService = journeyService;
        }

        // ---- formatting helpers ---------------------------------------------------------------

        private static string FormatIso(DateTime dt)
        {
            // Normalize to UTC ISO-8601. If Kind is Unspecified, treat as UTC.
            var utc = dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt.ToUniversalTime();
            return utc.ToString("O");
        }

        private static string FormatIsoOrEmpty(DateTime dt)
            => dt == default ? "" : FormatIso(dt);

        // CSV escape + guard against Excel formula injection
        private static string CsvSafe(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\"", "\"\""); // escape quotes
            // Guard against =, +, -, @ at start (Excel formula injection)
            if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@'))
                s = "'" + s;
            var needsQuotes = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            return needsQuotes ? $"\"{s}\"" : s;
        }

        // Put a clickable link into a cell using the HYPERLINK() formula (works on all ClosedXML versions)
        private static void SetHyperlinkFormula(IXLCell cell, string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                cell.Value = "";
                return;
            }
            var safe = url.Replace("\"", "\"\"");
            cell.SetFormulaA1($"HYPERLINK(\"{safe}\",\"{safe}\")");
        }

        // ---- CSV ------------------------------------------------------------------------------

        public async Task<ExportResult> ExportJourneyCsvAsync(
            Guid campaignSendLogId,
            CancellationToken ct = default)
        {
            var dto = await _journeyService.GetJourneyEventsAsync(campaignSendLogId, ct);

            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine("Timestamp,Source,EventType,Title,Details,Url,StepName,ButtonIndex,ButtonTitle,TemplateName,CampaignType,FlowName,FlowId,CampaignId,ContactId,ContactPhone");

            // ContactId may be non-nullable Guid in your DTO; emit empty when Guid.Empty
            string contactIdCsv = dto.ContactId == Guid.Empty ? "" : dto.ContactId.ToString();

            foreach (var e in dto.Events.OrderBy(x => x.Timestamp))
            {
                var line = string.Join(",",
                    CsvSafe(FormatIsoOrEmpty(e.Timestamp)),
                    CsvSafe(e.Source),
                    CsvSafe(e.EventType),
                    CsvSafe(e.Title),
                    CsvSafe(e.Details),
                    CsvSafe(e.Url),
                    CsvSafe(e.StepName),
                    e.ButtonIndex.HasValue ? e.ButtonIndex.Value.ToString() : "",
                    CsvSafe(e.ButtonTitle),
                    CsvSafe(e.TemplateName),
                    CsvSafe(dto.CampaignType),
                    CsvSafe(dto.FlowName),
                    dto.FlowId?.ToString() ?? "",
                    dto.CampaignId.ToString(),
                    contactIdCsv,
                    CsvSafe(dto.ContactPhone)
                );
                sb.AppendLine(line);
            }

            return new ExportResult
            {
                Bytes = Encoding.UTF8.GetBytes(sb.ToString()),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"journey-{campaignSendLogId}.csv"
            };
        }

        // ---- XLSX -----------------------------------------------------------------------------

        public async Task<ExportResult> ExportJourneyXlsxAsync(
            Guid campaignSendLogId,
            CancellationToken ct = default)
        {
            var dto = await _journeyService.GetJourneyEventsAsync(campaignSendLogId, ct);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Journey");

            var headers = new[]
            {
                "Timestamp","Source","EventType","Title","Details","Url","StepName",
                "ButtonIndex","ButtonTitle","TemplateName","CampaignType","FlowName",
                "FlowId","CampaignId","ContactId","ContactPhone"
            };

            // Header row
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");

            // Freeze header + auto filter
            ws.SheetView.FreezeRows(1);
            ws.Range(1, 1, 1, headers.Length).SetAutoFilter(); // avoid RangeUsed() on empty sheets

            // Data rows
            int r = 1;
            foreach (var e in dto.Events.OrderBy(x => x.Timestamp))
            {
                r++;
                int c = 1;

                ws.Cell(r, c++).Value = FormatIsoOrEmpty(e.Timestamp);
                ws.Cell(r, c++).Value = e.Source ?? "";
                ws.Cell(r, c++).Value = e.EventType ?? "";
                ws.Cell(r, c++).Value = e.Title ?? "";
                ws.Cell(r, c++).Value = e.Details ?? "";

                // URL
                SetHyperlinkFormula(ws.Cell(r, c), e.Url);
                c++;

                ws.Cell(r, c++).Value = e.StepName ?? "";
                ws.Cell(r, c++).Value = e.ButtonIndex?.ToString() ?? "";
                ws.Cell(r, c++).Value = e.ButtonTitle ?? "";
                ws.Cell(r, c++).Value = e.TemplateName ?? "";
                ws.Cell(r, c++).Value = dto.CampaignType ?? "";
                ws.Cell(r, c++).Value = dto.FlowName ?? "";
                ws.Cell(r, c++).Value = dto.FlowId?.ToString() ?? "";
                ws.Cell(r, c++).Value = dto.CampaignId.ToString();
                ws.Cell(r, c++).Value = (dto.ContactId == Guid.Empty ? "" : dto.ContactId.ToString());
                ws.Cell(r, c++).Value = dto.ContactPhone ?? "";
            }

            // Column sizing
            ws.Columns().AdjustToContents();
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 28); // timestamp column

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var bytes = ms.ToArray();

            return new ExportResult
            {
                Bytes = bytes,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"journey-{campaignSendLogId}.xlsx"
            };
        }
    }
}
