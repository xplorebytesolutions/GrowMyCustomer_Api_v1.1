using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.AccountInsights.DTOs;
using xbytechat.api.Features.AccountInsights.Models;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.PlanManagement.Models; // PlanType
using xbytechat.api.Models.BusinessModel;          // BusinessPlanInfo

namespace xbytechat.api.Features.AccountInsights.Services
{
    public class AccountInsightsService : IAccountInsightsService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AccountInsightsService> _log;

        private const int TrialExpiringSoonDays = 3;
        private const int DefaultTrialDaysFallback = 14;

        public AccountInsightsService(AppDbContext db, ILogger<AccountInsightsService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<AccountInsightsSnapshotDto> GetSnapshotAsync(Guid businessId)
        {
            var biz = await _db.Businesses
                .Include(b => b.BusinessPlanInfo)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (biz == null)
                throw new InvalidOperationException($"Business {businessId} not found");

            return await BuildSnapshotAsync(biz);
        }

        public async Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetSnapshotsAsync(
            int page = 1,
            int pageSize = 50,
            Guid? partnerId = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 500) pageSize = 50;

            var query = _db.Businesses
                .Include(b => b.BusinessPlanInfo)
                .AsQueryable();

            if (partnerId.HasValue)
            {
                query = query.Where(b => b.CreatedByPartnerId == partnerId.Value);
            }

            query = query.OrderByDescending(b => b.CreatedAt);

            var list = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var snapshots = new List<AccountInsightsSnapshotDto>(list.Count);
            foreach (var biz in list)
            {
                snapshots.Add(await BuildSnapshotAsync(biz));
            }

            return snapshots;
        }

        // ---------- Core snapshot builder ----------

        private async Task<AccountInsightsSnapshotDto> BuildSnapshotAsync(Business biz)
        {
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);
            var ninetyDaysAgo = now.AddDays(-90);

            var hasWaConfig = await _db.WhatsAppSettings
                .AnyAsync(x => x.BusinessId == biz.Id && x.IsActive);

            var hasActiveWaNumber = await _db.WhatsAppPhoneNumbers
                .AnyAsync(x => x.BusinessId == biz.Id && x.IsActive);

            var msgQuery = _db.MessageLogs.Where(m => m.BusinessId == biz.Id);
            var hasAnyMessages = await msgQuery.AnyAsync();

            DateTime? firstMessageAt = null;
            DateTime? lastMessageAt = null;
            var messagesLast30 = 0;

            if (hasAnyMessages)
            {
                firstMessageAt = await msgQuery.MinAsync(m => (DateTime?)m.CreatedAt);
                lastMessageAt = await msgQuery.MaxAsync(m => (DateTime?)m.CreatedAt);
                messagesLast30 = await msgQuery.CountAsync(m => m.CreatedAt >= thirtyDaysAgo);
            }

            var planInfo = biz.BusinessPlanInfo;

            // ---- Trial derivation ----
            bool isTrial = false;
            DateTime? trialStart = null;
            DateTime? trialEnd = null;
            bool trialExpiringSoon = false;
            bool trialExpired = false;

            if (planInfo != null &&
                planInfo.Plan == PlanType.Trial &&
                !biz.IsDeleted)
            {
                isTrial = true;

                trialStart = planInfo.CreatedAt != default
                    ? planInfo.CreatedAt
                    : biz.CreatedAt;

                if (trialStart.HasValue)
                {
                    if (planInfo.QuotaResetDate != default &&
                        planInfo.QuotaResetDate > trialStart.Value)
                    {
                        trialEnd = planInfo.QuotaResetDate;
                    }
                    else
                    {
                        trialEnd = trialStart.Value.AddDays(DefaultTrialDaysFallback);
                    }
                }

                if (trialEnd.HasValue)
                {
                    if (now <= trialEnd.Value)
                    {
                        var daysLeft = (trialEnd.Value - now).TotalDays;
                        if (daysLeft >= 0 && daysLeft <= TrialExpiringSoonDays)
                            trialExpiringSoon = true;
                    }
                    else
                    {
                        trialExpired = true;
                    }
                }
            }

            var stage = ComputeLifecycleStage(
                biz,
                hasWaConfig,
                hasActiveWaNumber,
                hasAnyMessages,
                lastMessageAt,
                messagesLast30,
                now,
                ninetyDaysAgo);

            var health = ComputeHealthScore(
                stage,
                hasWaConfig,
                hasActiveWaNumber,
                messagesLast30,
                lastMessageAt,
                now);

            return new AccountInsightsSnapshotDto
            {
                BusinessId = biz.Id,
                BusinessName = biz.BusinessName,
                BusinessEmail = biz.BusinessEmail,
                IsDeleted = biz.IsDeleted,
                Status = biz.Status.ToString(),
                IsApproved = biz.IsApproved,
                CreatedByPartnerId = biz.CreatedByPartnerId,
                CreatedAt = biz.CreatedAt,
                ApprovedAt = biz.ApprovedAt,

                PlanType = planInfo?.Plan,
                TotalMonthlyQuota = planInfo?.TotalMonthlyQuota,
                RemainingMessages = planInfo?.RemainingMessages,
                QuotaResetDate = planInfo?.QuotaResetDate,

                HasWhatsAppConfig = hasWaConfig,
                HasActiveWhatsAppNumber = hasActiveWaNumber,

                HasAnyMessages = hasAnyMessages,
                FirstMessageAt = firstMessageAt,
                LastMessageAt = lastMessageAt,
                MessagesLast30Days = messagesLast30,

                LifecycleStage = stage,
                HealthScore = health,

                IsTrial = isTrial,
                TrialStartAt = trialStart,
                TrialEndsAt = trialEnd,
                IsTrialExpiringSoon = trialExpiringSoon,
                IsTrialExpired = trialExpired
            };
        }

        private static AccountLifecycleStage ComputeLifecycleStage(
            Business biz,
            bool hasWaConfig,
            bool hasActiveWaNumber,
            bool hasAnyMessages,
            DateTime? lastMessageAt,
            int messagesLast30,
            DateTime now,
            DateTime ninetyDaysAgo)
        {
            if (biz.IsDeleted)
                return AccountLifecycleStage.InactiveDeleted;

            switch (biz.Status)
            {
                case Business.StatusType.Rejected:
                    return AccountLifecycleStage.Rejected;
                case Business.StatusType.Pending:
                    return AccountLifecycleStage.PendingApproval;
            }

            if (!biz.IsApproved)
                return AccountLifecycleStage.Unknown;

            if (!hasAnyMessages)
                return AccountLifecycleStage.NoUsagePostApproval;

            if (messagesLast30 > 0)
                return AccountLifecycleStage.Active;

            if (lastMessageAt.HasValue && lastMessageAt.Value < ninetyDaysAgo)
                return AccountLifecycleStage.Dormant;

            return AccountLifecycleStage.AtRisk;
        }

        private static int ComputeHealthScore(
            AccountLifecycleStage stage,
            bool hasWaConfig,
            bool hasActiveWaNumber,
            int messagesLast30,
            DateTime? lastMessageAt,
            DateTime now)
        {
            if (stage == AccountLifecycleStage.InactiveDeleted ||
                stage == AccountLifecycleStage.Rejected)
                return 0;

            var score = 0;

            if (hasWaConfig) score += 15;
            if (hasActiveWaNumber) score += 15;

            if (messagesLast30 > 0)
            {
                score += 40;
                if (messagesLast30 > 50) score += 10;
                if (messagesLast30 > 200) score += 10;
            }
            else if (lastMessageAt.HasValue)
            {
                var days = (now - lastMessageAt.Value).TotalDays;
                if (days <= 30) score += 30;
                else if (days <= 90) score += 15;
            }

            switch (stage)
            {
                case AccountLifecycleStage.Active:
                    score += 20;
                    break;
                case AccountLifecycleStage.AtRisk:
                    score += 5;
                    break;
                case AccountLifecycleStage.NoUsagePostApproval:
                    score -= 10;
                    break;
                case AccountLifecycleStage.Dormant:
                    score -= 20;
                    break;
            }

            if (score < 0) score = 0;
            if (score > 100) score = 100;

            return score;
        }

        // ---------- Summary ----------

        public async Task<AccountInsightsSummaryDto> GetSummaryAsync(Guid? partnerId = null)
        {
            var now = DateTime.UtcNow;

            var bizQuery = _db.Businesses
                .Include(b => b.BusinessPlanInfo)
                .AsQueryable();

            if (partnerId.HasValue)
                bizQuery = bizQuery.Where(b => b.CreatedByPartnerId == partnerId.Value);

            var businesses = await bizQuery.ToListAsync();

            var summary = new AccountInsightsSummaryDto
            {
                GeneratedAtUtc = now,
                TotalBusinesses = businesses.Count
            };

            var stageCounts = new Dictionary<AccountLifecycleStage, int>();
            var planCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var biz in businesses)
            {
                var snapshot = await BuildSnapshotAsync(biz);

                if (snapshot.IsDeleted)
                    summary.Deleted++;

                switch (snapshot.Status)
                {
                    case nameof(Business.StatusType.Pending):
                        summary.PendingApproval++;
                        break;
                    case nameof(Business.StatusType.Rejected):
                        summary.Rejected++;
                        break;
                }

                if (!stageCounts.ContainsKey(snapshot.LifecycleStage))
                    stageCounts[snapshot.LifecycleStage] = 0;
                stageCounts[snapshot.LifecycleStage]++;

                switch (snapshot.LifecycleStage)
                {
                    case AccountLifecycleStage.Active:
                        summary.ActiveBusinesses++;
                        break;
                    case AccountLifecycleStage.AtRisk:
                        summary.AtRiskBusinesses++;
                        break;
                    case AccountLifecycleStage.Dormant:
                        summary.DormantBusinesses++;
                        break;
                    case AccountLifecycleStage.NoUsagePostApproval:
                        summary.NoUsagePostApproval++;
                        break;
                }

                if (snapshot.PlanType.HasValue)
                {
                    var planKey = snapshot.PlanType.Value.ToString();
                    if (!planCounts.ContainsKey(planKey))
                        planCounts[planKey] = 0;
                    planCounts[planKey]++;

                    if (snapshot.PlanType.Value == PlanType.Trial)
                        summary.TrialPlan++;
                    else
                        summary.PaidPlan++;
                }
                else
                {
                    summary.UnknownPlan++;
                    if (!planCounts.ContainsKey("Unknown"))
                        planCounts["Unknown"] = 0;
                    planCounts["Unknown"]++;
                }

                if (snapshot.IsTrial)
                {
                    summary.TrialTotal++;

                    if (snapshot.IsTrialExpiringSoon &&
                        !snapshot.IsDeleted &&
                        snapshot.LifecycleStage != AccountLifecycleStage.Rejected)
                    {
                        summary.TrialExpiringSoon++;
                    }

                    if (snapshot.IsTrialExpired &&
                        !snapshot.IsDeleted &&
                        snapshot.PlanType == PlanType.Trial)
                    {
                        summary.TrialExpiredNoUpgrade++;
                    }
                }
            }

            foreach (var kv in stageCounts)
                summary.ByLifecycleStage[kv.Key.ToString()] = kv.Value;

            foreach (var kv in planCounts)
                summary.ByPlanType[kv.Key] = kv.Value;

            return summary;
        }

        // ---------- Queries used by AlertService ----------

        public async Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetTrialsExpiringSoonAsync(int days = TrialExpiringSoonDays)
        {
            if (days <= 0) days = TrialExpiringSoonDays;

            var now = DateTime.UtcNow;
            var maxDate = now.AddDays(days);

            var trials = await _db.Businesses
                .Include(b => b.BusinessPlanInfo)
                .Where(b =>
                    !b.IsDeleted &&
                    b.BusinessPlanInfo != null &&
                    b.BusinessPlanInfo.Plan == PlanType.Trial)
                .ToListAsync();

            var list = new List<AccountInsightsSnapshotDto>();

            foreach (var biz in trials)
            {
                var snapshot = await BuildSnapshotAsync(biz);

                if (snapshot.IsTrial &&
                    snapshot.TrialEndsAt.HasValue &&
                    snapshot.TrialEndsAt.Value >= now &&
                    snapshot.TrialEndsAt.Value <= maxDate &&
                    !snapshot.IsTrialExpired)
                {
                    list.Add(snapshot);
                }
            }

            return list;
        }

        public async Task<IReadOnlyList<AccountInsightsSnapshotDto>> GetByLifecycleStageAsync(
            AccountLifecycleStage stage,
            Guid? partnerId = null,
            int page = 1,
            int pageSize = 100)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 500) pageSize = 100;

            var query = _db.Businesses
                .Include(b => b.BusinessPlanInfo)
                .AsQueryable();

            if (partnerId.HasValue)
                query = query.Where(b => b.CreatedByPartnerId == partnerId.Value);

            query = query.OrderByDescending(b => b.CreatedAt);

            var businesses = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<AccountInsightsSnapshotDto>();

            foreach (var biz in businesses)
            {
                var snapshot = await BuildSnapshotAsync(biz);
                if (snapshot.LifecycleStage == stage)
                    result.Add(snapshot);
            }

            return result;
        }

        public async Task<IReadOnlyList<AccountInsightsActionDto>> GetRecentActionsAsync(
        Guid businessId,
        int limit = 10)
        {
            if (limit <= 0 || limit > 100)
                limit = 10;

            var actions = await _db.AccountInsightsActions
                .Where(a => a.BusinessId == businessId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(limit)
                .ToListAsync();

            var dtos = actions
                .Select(a => new AccountInsightsActionDto
                {
                    Id = a.Id,
                    BusinessId = a.BusinessId,
                    Type = a.ActionType,
                    Label = a.Label,
                    Actor = a.Actor,
                    MetaJson = a.MetaJson,
                    CreatedAt = a.CreatedAtUtc
                })
                .ToList();

            return dtos;
        }

        public async Task LogActionAsync(
    Guid businessId,
    string actionType,
    string label,
    string actor,
    string metaJson = null)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required", nameof(businessId));

            if (string.IsNullOrWhiteSpace(actionType))
                throw new ArgumentException("ActionType is required", nameof(actionType));

            if (string.IsNullOrWhiteSpace(label))
                label = actionType;

            var safeActor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();

            var entity = new AccountInsightsAction
            {
                BusinessId = businessId,
                ActionType = actionType,
                Label = label,
                Actor = safeActor,
                MetaJson = metaJson ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.AccountInsightsActions.Add(entity);
            await _db.SaveChangesAsync();

            _log.LogInformation(
                "AccountInsightsAction logged: {BusinessId} {Type} by {Actor}",
                businessId,
                actionType,
                safeActor);
        }

    }
}
