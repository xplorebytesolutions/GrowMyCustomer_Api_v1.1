using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Auditing.FlowExecutions.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Models;

namespace xbytechat.api.Features.Auditing.FlowExecutions.Services
{
    /// <summary>
    /// Default implementation of IFlowExecutionQueryService.
    /// Performs filtered, read-only queries over FlowExecutionLogs.
    /// </summary>
    public sealed class FlowExecutionQueryService : IFlowExecutionQueryService
    {
        private readonly AppDbContext _db;

        public FlowExecutionQueryService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IReadOnlyList<FlowExecutionLogDto>> GetRecentExecutionsAsync(
            Guid businessId,
            FlowExecutionFilter filter,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId must be a non-empty GUID.", nameof(businessId));

            filter ??= new FlowExecutionFilter();

            // Hard cap to avoid accidental huge result sets
            var limit = filter.Limit <= 0 ? 50 : filter.Limit;
            if (limit > 500)
            {
                limit = 500;
            }

            var query = _db.FlowExecutionLogs
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId);

            if (filter.Origin.HasValue)
            {
                query = query.Where(x => x.Origin == filter.Origin.Value);
            }

            if (filter.FlowId.HasValue)
            {
                var flowId = filter.FlowId.Value;
                // Entity FlowId is Guid?, so we just compare directly.
                query = query.Where(x => x.FlowId == flowId);
            }

            if (!string.IsNullOrWhiteSpace(filter.ContactPhone))
            {
                var phone = filter.ContactPhone.Trim();
                query = query.Where(x => x.ContactPhone == phone);
            }

            // Order by latest execution first, using FlowExecutionLog.ExecutedAt
            query = query
                .OrderByDescending(x => x.ExecutedAt)
                .ThenByDescending(x => x.Id)
                .Take(limit);

            var results = await query
                .Select(x => new FlowExecutionLogDto
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    RunId = x.RunId,
                    FlowId = x.FlowId,
                    AutoReplyFlowId = x.AutoReplyFlowId,
                    CampaignId = x.CampaignId,
                    CampaignSendLogId = x.CampaignSendLogId,
                    TrackingLogId = x.TrackingLogId,
                    Origin = x.Origin,
                    ContactPhone = x.ContactPhone,
                    StepId = x.StepId,
                    StepName = x.StepName,
                    TriggeredByButton = x.TriggeredByButton,
                    TemplateName = x.TemplateName,
                    TemplateType = x.TemplateType,
                    Success = x.Success,
                    ErrorMessage = x.ErrorMessage,
                    RawResponse = x.RawResponse,
                    MessageLogId = x.MessageLogId,
                    ButtonIndex = x.ButtonIndex,
                    RequestId = x.RequestId,
                    ExecutedAtUtc = x.ExecutedAt
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return results;
        }

        //public async Task<IReadOnlyList<FlowExecutionLogDto>> GetRunTimelineAsync(
        //    Guid businessId,
        //    Guid runId,
        //    CancellationToken ct = default)
        //{
        //    if (businessId == Guid.Empty)
        //        throw new ArgumentException("BusinessId is required", nameof(businessId));

        //    if (runId == Guid.Empty)
        //        throw new ArgumentException("RunId is required", nameof(runId));

        //    var query = _db.FlowExecutionLogs
        //        .AsNoTracking()
        //        .Where(x =>
        //            x.BusinessId == businessId &&
        //            x.RunId == runId);

        //    // For a timeline we want oldest → newest.
        //    query = query
        //        .OrderBy(x => x.ExecutedAt)
        //        .ThenBy(x => x.Id);

        //    return await query
        //        .Select(x => new FlowExecutionLogDto
        //        {
        //            Id = x.Id,
        //            RunId = x.RunId,
        //            BusinessId = x.BusinessId,
        //            FlowId = x.FlowId,
        //            AutoReplyFlowId = x.AutoReplyFlowId,
        //            CampaignId = x.CampaignId,
        //            Origin = x.Origin,

        //            StepId = x.StepId,
        //            StepName = x.StepName,
        //            ContactPhone = x.ContactPhone,
        //            TriggeredByButton = x.TriggeredByButton,
        //            TemplateName = x.TemplateName,
        //            TemplateType = x.TemplateType,
        //            Success = x.Success,
        //            ErrorMessage = x.ErrorMessage,
        //            RawResponse = x.RawResponse,
        //            MessageLogId = x.MessageLogId,
        //            ButtonIndex = x.ButtonIndex,
        //            RequestId = x.RequestId,
        //            ExecutedAtUtc = x.ExecutedAt
        //        })
        //        .ToListAsync(ct)
        //        .ConfigureAwait(false);
        //}
        public async Task<IReadOnlyList<FlowExecutionLogDto>> GetRunTimelineAsync(
            Guid businessId,
            Guid runId,
            CancellationToken ct = default)
        {
            if (runId == Guid.Empty)
                throw new ArgumentException("RunId is required", nameof(runId));

            // Primary query: business-scoped (what we expect normally)
            IQueryable<FlowExecutionLog> query = _db.FlowExecutionLogs
                .AsNoTracking()
                .Where(x => x.RunId == runId);

            if (businessId != Guid.Empty)
            {
                query = query.Where(x => x.BusinessId == businessId);
            }

            var primary = await query
                .OrderBy(x => x.ExecutedAt)
                .ThenBy(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // If nothing found, fall back to "by RunId only" (no business filter).
            // This makes the explorer robust against any mismatched BusinessId data.
            List<FlowExecutionLog> rows;
            if (primary.Count > 0)
            {
                rows = primary;
            }
            else
            {
                rows = await _db.FlowExecutionLogs
                    .AsNoTracking()
                    .Where(x => x.RunId == runId)
                    .OrderBy(x => x.ExecutedAt)
                    .ThenBy(x => x.Id)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }

            return rows
                .Select(x => new FlowExecutionLogDto
                {
                    Id = x.Id,
                    RunId = x.RunId,
                    BusinessId = x.BusinessId,
                    FlowId = x.FlowId,
                    AutoReplyFlowId = x.AutoReplyFlowId,
                    CampaignId = x.CampaignId,
                    CampaignSendLogId = x.CampaignSendLogId,
                    TrackingLogId = x.TrackingLogId,
                    Origin = x.Origin,
                    StepId = x.StepId,
                    StepName = x.StepName,
                    ContactPhone = x.ContactPhone,
                    TriggeredByButton = x.TriggeredByButton,
                    TemplateName = x.TemplateName,
                    TemplateType = x.TemplateType,
                    Success = x.Success,
                    ErrorMessage = x.ErrorMessage,
                    RawResponse = x.RawResponse,
                    MessageLogId = x.MessageLogId,
                    ButtonIndex = x.ButtonIndex,
                    RequestId = x.RequestId,
                    ExecutedAtUtc = x.ExecutedAt
                })
                .ToList();
        }
    }
}
