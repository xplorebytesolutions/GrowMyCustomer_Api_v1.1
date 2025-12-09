// 📄 Features/PlanManagement/Services/PlanManager.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Entitlements;
using xbytechat.api.Features.Entitlements.Services;
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Helpers;
using xbytechat.api.Models.BusinessModel;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.PlanManagement.Services
{
    public class PlanManager : IPlanManager
    {
        private readonly AppDbContext _db;
        private readonly IQuotaService _quota;

        public PlanManager(AppDbContext db, IQuotaService quota)
        {
            _db = db;
            _quota = quota;
        }

        /// <summary>
        /// Check and CONSUME 1 unit of "messages per month" quota for this business.
        /// If not allowed, returns a ResponseResult with a user-friendly error.
        /// </summary>
        public async Task<ResponseResult> CheckQuotaBeforeSendingAsync(Guid businessId)
        {
            // Load plan info for better error messaging (Trial vs Paid)
            var business = await _db.Businesses
                .Include(b => b.BusinessPlanInfo)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business is null)
            {
                return ResponseResult.ErrorInfo(
                    "Business not found.",
                    "Invalid business ID in CheckQuotaBeforeSendingAsync.");
            }

            var planType = business.BusinessPlanInfo?.Plan ?? PlanType.Trial;

            // Use the generic quota engine
            var result = await _quota.CheckAndConsumeAsync(
                businessId,
                QuotaKeys.MessagesPerMonth,
                amount: 1,
                ct: CancellationToken.None);

            if (!result.Allowed)
            {
                // 1) Prefer denial text coming from PlanQuotas / overrides
                var userMessage = !string.IsNullOrWhiteSpace(result.Message)
                    ? result.Message!
                    // 2) Fallback to your old behavior
                    : planType == PlanType.Trial
                        ? "Trial limit reached. Please upgrade your plan."
                        : "Monthly message quota exhausted. Please upgrade or wait for reset.";

                return ResponseResult.ErrorInfo(
                    userMessage,
                    $"Quota exceeded for {QuotaKeys.MessagesPerMonth}");
            }

            // OK → caller (message send service) can proceed to send
            return ResponseResult.SuccessInfo("Quota check passed.");
        }

        /// <summary>
        /// Legacy feature map per plan.
        /// You can keep this for now; later we can align it with Permission/Entitlements.
        /// </summary>
        public Dictionary<string, bool> GetPlanFeatureMap(string plan)
        {
            // Keep your previous behavior here.
            // If your old code had slightly different maps, just plug them in.

            switch (plan?.Trim().ToLowerInvariant())
            {
                case "basic":
                    return new Dictionary<string, bool>
                    {
                        { "CATALOG", true },
                        { "MESSAGE_SEND", true },
                        { "CRM_NOTES", true },
                        { "CRM_TAGS", true }
                    };

                case "smart":
                    return new Dictionary<string, bool>
                    {
                        { "CATALOG", true },
                        { "MESSAGE_SEND", true },
                        { "CRM_NOTES", true },
                        { "CRM_TAGS", true }
                    };

                case "advanced":
                    return new Dictionary<string, bool>
                    {
                        { "CATALOG", true },
                        { "MESSAGE_SEND", true },
                        { "CRM_NOTES", true },
                        { "CRM_TAGS", true }
                    };

                default:
                    return new Dictionary<string, bool>();
            }
        }
    }
}
