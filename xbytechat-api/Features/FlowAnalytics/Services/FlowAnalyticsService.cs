using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.FlowAnalytics.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.FlowAnalytics.Services
{
    public class FlowAnalyticsService : IFlowAnalyticsService
    {
        private readonly AppDbContext _context;

        public FlowAnalyticsService(AppDbContext context)
        {
            _context = context;
        }

        // ✅ Summary cards (executions, unique contacts, top step)
        public async Task<FlowAnalyticsSummaryDto> GetAnalyticsSummaryAsync(Guid businessId)
        {
            try
            {
                Log.Information("📊 Generating Flow Analytics Summary for BusinessId: {BusinessId}", businessId);

                var recentExecutions = await _context.FlowExecutionLogs
                    .Where(e => e.BusinessId == businessId)
                    .ToListAsync();

                var totalExecutions = recentExecutions.Count;
                var uniqueContacts = recentExecutions.Select(e => e.ContactPhone).Distinct().Count();
                var mostTriggeredStep = recentExecutions
                    .GroupBy(e => e.StepName)
                    .OrderByDescending(g => g.Count())
                    .Select(g => new { Step = g.Key, Count = g.Count() })
                    .FirstOrDefault();

                return new FlowAnalyticsSummaryDto
                {
                    TotalExecutions = totalExecutions,
                    UniqueContacts = uniqueContacts,
                    TopStepTriggered = mostTriggeredStep?.Step ?? "N/A",
                    TopStepCount = mostTriggeredStep?.Count ?? 0,
                    LastExecutedAt = recentExecutions
                        .OrderByDescending(e => e.ExecutedAt)
                        .FirstOrDefault()?.ExecutedAt
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to generate flow analytics summary");
                return new FlowAnalyticsSummaryDto();
            }
        }

        // ✅ Top triggered steps leaderboard
        public async Task<List<MostTriggeredStepDto>> GetMostTriggeredStepsAsync(Guid businessId)
        {
            return await _context.FlowExecutionLogs
                .Where(e => e.BusinessId == businessId)
                .GroupBy(e => new { e.StepId, e.StepName })
                .Select(g => new MostTriggeredStepDto
                {
                    StepId = g.Key.StepId,
                    StepName = g.Key.StepName,
                    TriggerCount = g.Count(),
                    LastTriggeredAt = g.Max(e => e.ExecutedAt)
                })
                .OrderByDescending(x => x.TriggerCount)
                .Take(5)
                .ToListAsync();
        }

        // ✅ Step-by-step journey breakdown (with date filter)
        public async Task<List<FlowAnalyticsStepJourneyDto>> GetStepJourneyBreakdownAsync(Guid businessId, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.FlowExecutionLogs
                .Where(e => e.BusinessId == businessId);

            if (startDate.HasValue)
                query = query.Where(e => e.ExecutedAt >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(e => e.ExecutedAt <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            var logs = await query.ToListAsync();

            // Group by StepId
            var grouped = logs
                .GroupBy(e => new { e.StepId, e.TemplateName, e.TriggeredByButton, e.FlowId })
                .Select(g => new
                {
                    StepId = g.Key.StepId,
                    TemplateName = g.Key.TemplateName,
                    TotalReached = g.Count(),
                    ClickedNext = logs.Count(x =>
                        x.TriggeredByButton != null &&
                        x.FlowId == g.Key.FlowId &&
                        x.StepId != g.Key.StepId &&
                        x.TriggeredByButton == g.Key.TriggeredByButton
                    ),
                    FlowId = g.Key.FlowId,
                    TriggeredByButton = g.Key.TriggeredByButton
                })
                .ToList();

            // Build final breakdown
            var breakdown = grouped.Select(g => new FlowAnalyticsStepJourneyDto
            {
                StepId = g.StepId,
                TemplateName = g.TemplateName,
                TotalReached = g.TotalReached,
                ClickedNext = g.ClickedNext,
              //  DropOff = g.TotalReached - g.ClickedNext,
                NextStepId = _context.FlowButtonLinks
                    .Where(bl =>
                        bl.CTAFlowStepId == g.StepId &&
                        bl.ButtonText == g.TriggeredByButton)
                    .Select(bl => bl.NextStepId)
                    .FirstOrDefault()
            }).ToList();

            return breakdown;
        }
    }
}
