using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Features.CampaignModule.DTOs;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICsvExportService
    {
        Task<byte[]> BuildMaterializedCsvAsync(Guid businessId, Guid campaignId, int limit = 200, CancellationToken ct = default);
        Task<byte[]> BuildDispatchPlanCsvAsync(Guid businessId, Guid campaignId, int limit = 2000, CancellationToken ct = default);
    }

    /// <summary>
    /// Small CSV builder for exporting materialized rows and dispatch plans.
    /// Uses UTF-8 with BOM for Excel friendliness. Escapes fields per RFC4180.
    /// </summary>
    public class CsvExportService : ICsvExportService
    {
        private readonly ICampaignMaterializationService _materializer;
        private readonly ICampaignDispatchPlannerService _planner;

        public CsvExportService(
            ICampaignMaterializationService materializer,
            ICampaignDispatchPlannerService planner)
        {
            _materializer = materializer;
            _planner = planner;
        }

        public async Task<byte[]> BuildMaterializedCsvAsync(Guid businessId, Guid campaignId, int limit = 200, CancellationToken ct = default)
        {
            var data = await _materializer.MaterializeAsync(businessId, campaignId, limit, ct);

            // Header is dynamic based on placeholder count and button count.
            // Columns:
            // RecipientId,ContactId,Phone,Param1..ParamN,Btn1Text,Btn1Url,...,Warnings,Errors
            var maxParam = data.PlaceholderCount;
            var maxButtons = data.Rows.Max(r => r.Buttons.Count);

            var sb = new StringBuilder();
            using var writer = new StringWriter(sb);

            // Write header
            writer.Write("RecipientId,ContactId,Phone");
            for (int i = 1; i <= maxParam; i++) writer.Write($",Param{i}");
            for (int b = 1; b <= maxButtons; b++) writer.Write($",Btn{b}Text,Btn{b}Url");
            writer.Write(",Warnings,Errors");
            writer.WriteLine();

            foreach (var row in data.Rows)
            {
                WriteCsv(writer, row.RecipientId?.ToString());
                writer.Write(",");
                WriteCsv(writer, row.ContactId?.ToString());
                writer.Write(",");
                WriteCsv(writer, row.Phone);

                // Params 1..N (pad missing)
                for (int i = 1; i <= maxParam; i++)
                {
                    writer.Write(",");
                    var val = row.Parameters.FirstOrDefault(p => p.Index == i)?.Value;
                    WriteCsv(writer, val);
                }

                // Buttons (pad missing)
                for (int b = 0; b < maxButtons; b++)
                {
                    var btn = b < row.Buttons.Count ? row.Buttons[b] : null;
                    writer.Write(",");
                    WriteCsv(writer, btn?.ButtonText);
                    writer.Write(",");
                    WriteCsv(writer, btn?.ResolvedUrl);
                }

                writer.Write(",");
                WriteCsv(writer, string.Join(" | ", row.Warnings));
                writer.Write(",");
                WriteCsv(writer, string.Join(" | ", row.Errors));
                writer.WriteLine();
            }

            // Return as UTF-8 with BOM for Excel compatibility
            var utf8withBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return utf8withBom.GetBytes(sb.ToString());
        }

        public async Task<byte[]> BuildDispatchPlanCsvAsync(Guid businessId, Guid campaignId, int limit = 2000, CancellationToken ct = default)
        {
            var plan = await _planner.PlanAsync(businessId, campaignId, limit, ct);

            var sb = new StringBuilder();
            using var writer = new StringWriter(sb);

            // Plan metadata preface (comment-style rows start with '#')
            writer.WriteLine($"# CampaignId,{plan.CampaignId}");
            writer.WriteLine($"# TemplateName,{Escape(plan.TemplateName)}");
            writer.WriteLine($"# Language,{Escape(plan.Language)}");
            writer.WriteLine($"# PlaceholderCount,{plan.PlaceholderCount}");
            writer.WriteLine($"# TotalRecipients,{plan.TotalRecipients}");
            writer.WriteLine($"# ProviderPlan,{Escape(plan.Throttle.Plan)}");
            writer.WriteLine($"# Provider,{Escape(plan.Throttle.Provider)}");
            writer.WriteLine($"# MaxBatchSize,{plan.Throttle.MaxBatchSize}");
            writer.WriteLine($"# MaxPerMinute,{plan.Throttle.MaxPerMinute}");
            writer.WriteLine($"# ComputedBatches,{plan.Throttle.ComputedBatches}");
            writer.WriteLine($"# EstimatedMinutes,{plan.Throttle.EstimatedMinutes}");
            if (plan.GlobalWarnings.Any())
                writer.WriteLine($"# GlobalWarnings,{Escape(string.Join(" | ", plan.GlobalWarnings))}");
            if (plan.Throttle.Warnings.Any())
                writer.WriteLine($"# ThrottleWarnings,{Escape(string.Join(" | ", plan.Throttle.Warnings))}");

            writer.WriteLine(); // blank line

            // Batches table header
            writer.WriteLine("BatchNumber,OffsetSeconds,StartIndex,Count,ApproxBytes,Phones,RecipientIds,Notes");

            foreach (var b in plan.Batches)
            {
                WriteCsv(writer, b.BatchNumber.ToString());
                writer.Write(",");
                WriteCsv(writer, b.OffsetSeconds.ToString());
                writer.Write(",");
                WriteCsv(writer, b.StartIndex.ToString());
                writer.Write(",");
                WriteCsv(writer, b.Count.ToString());
                writer.Write(",");
                WriteCsv(writer, b.ApproxBytes.ToString());
                writer.Write(",");
                WriteCsv(writer, string.Join(" ", b.Phones.Select(p => p ?? "")));
                writer.Write(",");
                WriteCsv(writer, string.Join(" ", b.RecipientIds.Select(id => id?.ToString() ?? "")));
                writer.Write(",");
                WriteCsv(writer, string.Join(" | ", b.Notes));
                writer.WriteLine();
            }

            var utf8withBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return utf8withBom.GetBytes(sb.ToString());
        }

        private static void WriteCsv(TextWriter writer, string? value)
        {
            writer.Write(Escape(value ?? ""));
        }

        private static string Escape(string input)
        {
            // RFC4180-style: quote if contains comma, quote or newline; escape quotes by doubling
            var needsQuote = input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r');
            if (!needsQuote) return input;
            return $"\"{input.Replace("\"", "\"\"")}\"";
        }
    }
}
