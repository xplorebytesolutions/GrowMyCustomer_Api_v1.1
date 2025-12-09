using System;

namespace xbytechat.api.Features.CTAFlowBuilder.Models
{
    /// <summary>
    /// Canonical context/payload for logging a single flow step execution.
    /// This wraps all the information we want to write into FlowExecutionLogs.
    /// </summary>
    public sealed class FlowExecutionContext
    {
        /// <summary>
        /// Tenant / business that owns this flow execution.
        /// </summary>
        public Guid BusinessId { get; set; }

        /// <summary>
        /// Optional contact this journey is associated with.
        /// </summary>
        public Guid? ContactId { get; set; }   // 👈 ADD THIS

        /// <summary>
        /// Which engine started this journey (Campaign, AutoReply, etc.).
        /// </summary>
        public FlowExecutionOrigin Origin { get; set; } = FlowExecutionOrigin.Unknown;

        /// <summary>
        /// Logical flow definition. For CTA flows this is CTAFlowConfig.Id.
        /// </summary>
        public Guid? FlowId { get; set; }

        /// <summary>
        /// Step being executed.
        /// </summary>
        public Guid StepId { get; set; }

        /// <summary>
        /// Optional friendly name for the step.
        /// </summary>
        public string? StepName { get; set; }

        /// <summary>
        /// Optional correlation id for one "run" of a journey.
        /// Multiple steps in the same journey can share RunId.
        /// </summary>
        public Guid? RunId { get; set; }

        /// <summary>
        /// Optional higher-level campaign this journey belongs to.
        /// </summary>
        public Guid? CampaignId { get; set; }

        /// <summary>
        /// Optional AutoReplyFlow id when journey started from keyword matching.
        /// </summary>
        public Guid? AutoReplyFlowId { get; set; }

        /// <summary>
        /// Optional specific send log (CampaignSendLog) if this was tied to a blast.
        /// </summary>
        public Guid? CampaignSendLogId { get; set; }

        /// <summary>
        /// Optional tracking log id (CTA click tracking, etc.).
        /// </summary>
        public Guid? TrackingLogId { get; set; }

        /// <summary>
        /// Optional link to underlying MessageLog row.
        /// </summary>
        public Guid? MessageLogId { get; set; }

        /// <summary>
        /// Phone number in E.164 form that this step is interacting with.
        /// </summary>
        public string? ContactPhone { get; set; }

        /// <summary>
        /// Human-readable label of the button that triggered the step, if any.
        /// </summary>
        public string? TriggeredByButton { get; set; }

        /// <summary>
        /// Index of the clicked button (0..2) where applicable.
        /// </summary>
        public short? ButtonIndex { get; set; }

        /// <summary>
        /// Template name that was used in this step (if any).
        /// </summary>
        public string? TemplateName { get; set; }

        /// <summary>
        /// Template type / category (e.g. "image_template", "text_template").
        /// </summary>
        public string? TemplateType { get; set; }

        /// <summary>
        /// Per-request correlation id (can come from message engine, HTTP request, etc.).
        /// </summary>
        public Guid? RequestId { get; set; }

        /// <summary>
        /// Whether the step action completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message when the step failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Raw provider response or internal payload for debugging.
        /// </summary>
        public string? RawResponse { get; set; }

        /// <summary>
        /// When the step was executed (UTC).
        /// If null, the logger will default to DateTime.UtcNow.
        /// </summary>
        public DateTime? ExecutedAtUtc { get; set; }
    }
}
