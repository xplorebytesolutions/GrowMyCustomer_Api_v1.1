using System;
using xbytechat.api.Features.CTAFlowBuilder.Models;

namespace xbytechat.api.Features.Auditing.FlowExecutions.DTOs
{
    /// <summary>
    /// Lightweight projection of a FlowExecutionLog row for debugging/analytics.
    /// Matches the current FlowExecutionLog entity in CTAFlowBuilder.Models.
    /// </summary>
    public class FlowExecutionLogDto
    {
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }

        /// <summary>
        /// Correlation id for a single run of a flow (e.g. one user journey).
        /// Nullable because older rows might not have it.
        /// </summary>
        public Guid? RunId { get; set; }

        /// <summary>
        /// Flow id being executed:
        /// - For CTA flows: CTAFlowConfig.Id
        /// - For AutoReply flows: AutoReplyFlow.Id (depending on how you log it)
        /// Nullable because your entity uses Guid?.
        /// </summary>
        public Guid? FlowId { get; set; }

        /// <summary>
        /// If this execution was started by an AutoReply flow, this can carry that flow id.
        /// </summary>
        public Guid? AutoReplyFlowId { get; set; }

        /// <summary>
        /// If this execution was started as part of a campaign, this can carry Campaign.Id.
        /// </summary>
        public Guid? CampaignId { get; set; }

        /// <summary>
        /// Optional link to a specific campaign send log.
        /// </summary>
        public Guid? CampaignSendLogId { get; set; }

        /// <summary>
        /// Optional link to a tracking log row (e.g. button click tracking).
        /// </summary>
        public Guid? TrackingLogId { get; set; }

        public FlowExecutionOrigin Origin { get; set; }

        /// <summary>
        /// Contact's WhatsApp phone number (as stored in FlowExecutionLog).
        /// </summary>
        public string? ContactPhone { get; set; }

        public Guid StepId { get; set; }

        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// If the step was triggered by a button click, this can store the button text.
        /// </summary>
        public string? TriggeredByButton { get; set; }

        public string? TemplateName { get; set; }

        public string? TemplateType { get; set; }

        /// <summary>
        /// True if the step was executed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message when Success == false.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Raw provider response (optional).
        /// </summary>
        public string? RawResponse { get; set; }

        /// <summary>
        /// Optional link back to the originating MessageLog.
        /// </summary>
        public Guid? MessageLogId { get; set; }

        /// <summary>
        /// Which button index (0..2) was clicked, if applicable.
        /// </summary>
        public short? ButtonIndex { get; set; }

        /// <summary>
        /// Optional request id correlation (for cross-service tracing).
        /// </summary>
        public Guid? RequestId { get; set; }

        /// <summary>
        /// When this step was executed (UTC). Backed by FlowExecutionLog.ExecutedAt.
        /// </summary>
        public DateTime ExecutedAtUtc { get; set; }
    }
}
