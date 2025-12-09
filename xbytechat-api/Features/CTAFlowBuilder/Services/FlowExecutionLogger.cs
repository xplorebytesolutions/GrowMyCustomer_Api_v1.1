// 📄 xbytechat-api/Features/CTAFlowBuilder/Services/FlowExecutionLogger.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using xbytechat.api; // ✅ Needed so AppDbContext is in scope
using xbytechat.api.Features.CTAFlowBuilder.Models;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    /// <summary>
    /// Default implementation of IFlowExecutionLogger.
    /// Writes origin-tagged rows into FlowExecutionLogs.
    /// </summary>
    public sealed class FlowExecutionLogger : IFlowExecutionLogger
    {
        private readonly AppDbContext _db;

        public FlowExecutionLogger(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task LogStepAsync(FlowExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            try
            {
                var entity = new FlowExecutionLog
                {
                    Id = Guid.NewGuid(),

                    // core identifiers
                    RunId = context.RunId,
                    BusinessId = context.BusinessId,
                    FlowId = context.FlowId,
                    StepId = context.StepId,
                    StepName = context.StepName ?? string.Empty,

                    // origin + linkage
                    Origin = context.Origin,
                    CampaignId = context.CampaignId,
                    AutoReplyFlowId = context.AutoReplyFlowId,
                    CampaignSendLogId = context.CampaignSendLogId,
                    TrackingLogId = context.TrackingLogId,
                    MessageLogId = context.MessageLogId,

                    // contact + button context
                    ContactPhone = context.ContactPhone,
                    TriggeredByButton = context.TriggeredByButton,
                    ButtonIndex = context.ButtonIndex,

                    // template / execution info
                    TemplateName = context.TemplateName,
                    TemplateType = context.TemplateType,
                    Success = context.Success,
                    ErrorMessage = context.ErrorMessage,
                    RawResponse = context.RawResponse,

                    // timestamps + tracing
                    ExecutedAt = context.ExecutedAtUtc ?? DateTime.UtcNow,
                    RequestId = context.RequestId
                };

                _db.FlowExecutionLogs.Add(entity);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Never let logging failures break main flow execution.
                Log.Error(
                    ex,
                    "❌ Failed to write FlowExecutionLog | Biz={BusinessId} Origin={Origin} Flow={FlowId} Step={StepId}",
                    context.BusinessId,
                    context.Origin,
                    context.FlowId,
                    context.StepId
                );
            }
        }
    }
}


//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Serilog;
//using xbytechat.api.Features.AutoReplyBuilder.DTOs;
//using xbytechat.api.Features.CTAFlowBuilder.Models;

//namespace xbytechat.api.Features.CTAFlowBuilder.Services
//{
//    /// <summary>
//    /// Default implementation of IFlowExecutionLogger.
//    /// Writes origin-tagged rows into FlowExecutionLogs.
//    /// </summary>
//    public sealed class FlowExecutionLogger : IFlowExecutionLogger
//    {
//        private readonly AppDbContext _db;

//        public FlowExecutionLogger(AppDbContext db)
//        {
//            _db = db ?? throw new ArgumentNullException(nameof(db));
//        }

//        public async Task LogStepAsync(FlowExecutionContext context, CancellationToken cancellationToken = default)
//        {
//            if (context == null) throw new ArgumentNullException(nameof(context));

//            try
//            {
//                var entity = new FlowExecutionLog
//                {
//                    Id = Guid.NewGuid(),
//                    RunId = context.RunId,
//                    BusinessId = context.BusinessId,
//                    StepId = context.StepId,
//                    StepName = context.StepName ?? string.Empty,
//                    FlowId = context.FlowId,
//                    Origin = context.Origin,
//                    CampaignId = context.CampaignId,
//                    AutoReplyFlowId = context.AutoReplyFlowId,
//                    CampaignSendLogId = context.CampaignSendLogId,
//                    TrackingLogId = context.TrackingLogId,
//                    ContactPhone = context.ContactPhone,
//                    TriggeredByButton = context.TriggeredByButton,
//                    TemplateName = context.TemplateName,
//                    TemplateType = context.TemplateType,
//                    Success = context.Success,
//                    ErrorMessage = context.ErrorMessage,
//                    RawResponse = context.RawResponse,
//                    ExecutedAt = context.ExecutedAtUtc ?? DateTime.UtcNow,
//                    MessageLogId = context.MessageLogId,
//                    ButtonIndex = context.ButtonIndex,
//                    RequestId = context.RequestId
//                };

//                _db.FlowExecutionLogs.Add(entity);
//                await _db.SaveChangesAsync(cancellationToken);
//            }
//            catch (Exception ex)
//            {
//                // We never want logging failures to break the main flow.
//                Log.Error(ex,
//                    "❌ Failed to write FlowExecutionLog | Biz={BusinessId} Origin={Origin} Flow={FlowId} Step={StepId}",
//                    context.BusinessId,
//                    context.Origin,
//                    context.FlowId,
//                    context.StepId);
//            }
//        }
//    }
//}
