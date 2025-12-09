using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.AccountInsights.DTOs;
using xbytechat.api.Features.AccountInsights.Models;
using xbytechat.api.Features.AccountInsights.Services;

namespace xbytechat.api.Features.AccountInsights.Services
{
    public interface IAccountInsightsAlertService
    {
        Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetTrialExpiringSoonAsync(int days = 3);
        Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetInactivePostApprovalAsync();
    }

    /// <summary>
    /// Read-only helper for schedulers / workers to fetch cohorts that should be nudged.
    /// This layer does NOT send messages itself.
    /// </summary>
    public class AccountInsightsAlertService : IAccountInsightsAlertService
    {
        private readonly IAccountInsightsService _insights;
        private readonly ILogger<AccountInsightsAlertService> _log;

        public AccountInsightsAlertService(
            IAccountInsightsService insights,
            ILogger<AccountInsightsAlertService> log)
        {
            _insights = insights;
            _log = log;
        }

        public async Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetTrialExpiringSoonAsync(int days = 3)
        {
            // Uses core logic; safe to call from a daily/cron job.
            var list = await _insights.GetTrialsExpiringSoonAsync(days);
            _log.LogInformation("Found {Count} trials expiring in next {Days} days", list.Count, days);
            return list;
        }

        public async Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetInactivePostApprovalAsync()
        {
            // NoUsagePostApproval cohort: approved, no messages ever.
            var list = await _insights.GetByLifecycleStageAsync(AccountLifecycleStage.NoUsagePostApproval);
            _log.LogInformation("Found {Count} approved accounts with no usage post-approval", list.Count);
            return list;
        }
    }
}
