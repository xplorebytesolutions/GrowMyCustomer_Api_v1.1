using System;

namespace xbytechat.api.Features.AutoReplyBuilder.Services
{
    /// <summary>
    /// Result of trying to handle an incoming message via AutoReply.
    /// This is used by the webhook and by the test-match endpoint.
    /// </summary>
    public class AutoReplyRuntimeResult
    {
        /// <summary>
        /// True if this incoming message was handled by AutoReply logic
        /// (either by sending a simple reply or starting a CTA flow).
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// True if we sent an immediate/simple reply (text/template) as part of handling.
        /// </summary>
        public bool SentSimpleReply { get; set; }

        /// <summary>
        /// True if, instead of only replying once, we started a CTA flow journey.
        /// </summary>
        public bool StartedCtaFlow { get; set; }

        /// <summary>
        /// The AutoReply flow/rule that matched (if any).
        /// </summary>
        public Guid? AutoReplyFlowId { get; set; }

        /// <summary>
        /// If a CTA flow was started, this is the CTAFlowConfig.Id that was kicked off.
        /// </summary>
        public Guid? CtaFlowConfigId { get; set; }

        /// <summary>
        /// The keyword or pattern that matched, for debugging/analytics.
        /// </summary>
        public string? MatchedKeyword { get; set; }

        /// <summary>
        /// Optional notes for logging / debugging (e.g. why nothing matched).
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// (Test-mode only) Type of the first action node we plan to execute
        /// in the matched flow (e.g. "message", "wait", "set-tag").
        /// </summary>
        public string? StartNodeType { get; set; }

        /// <summary>
        /// (Test-mode only) Display name of the first action node.
        /// </summary>
        public string? StartNodeName { get; set; }

        // ---- Helper factories ----

        public static AutoReplyRuntimeResult NotHandled(string? notes = null) =>
            new AutoReplyRuntimeResult
            {
                Handled = false,
                SentSimpleReply = false,
                StartedCtaFlow = false,
                Notes = notes,
                AutoReplyFlowId = null,
                CtaFlowConfigId = null,
                MatchedKeyword = null,
                StartNodeType = null,
                StartNodeName = null
            };

        public static AutoReplyRuntimeResult SimpleReply(
            Guid? autoReplyFlowId,
            string? matchedKeyword = null,
            string? notes = null,
            string? startNodeType = null,
            string? startNodeName = null) =>
            new AutoReplyRuntimeResult
            {
                Handled = true,
                SentSimpleReply = true,
                StartedCtaFlow = false,
                AutoReplyFlowId = autoReplyFlowId,
                CtaFlowConfigId = null,
                MatchedKeyword = matchedKeyword,
                Notes = notes,
                StartNodeType = startNodeType,
                StartNodeName = startNodeName
            };

        public static AutoReplyRuntimeResult CtaFlowStarted(
            Guid? autoReplyFlowId,
            Guid? ctaFlowConfigId,
            string? matchedKeyword = null,
            string? notes = null,
            string? startNodeType = null,
            string? startNodeName = null) =>
            new AutoReplyRuntimeResult
            {
                Handled = true,
                SentSimpleReply = false,
                StartedCtaFlow = true,
                AutoReplyFlowId = autoReplyFlowId,
                CtaFlowConfigId = ctaFlowConfigId,
                MatchedKeyword = matchedKeyword,
                Notes = notes,
                StartNodeType = startNodeType,
                StartNodeName = startNodeName
            };
    }
}
