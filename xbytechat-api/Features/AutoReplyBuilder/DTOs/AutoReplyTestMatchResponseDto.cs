using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Response shape for POST /api/autoreplyflows/test-match.
    /// This is what the AutoReplyBuilder UI expects in testResult.
    /// </summary>
    public sealed class AutoReplyTestMatchResponseDto
    {
        /// <summary>
        /// True if a matching AutoReply flow was found for the given text.
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// The matched AutoReply flow ID, if any.
        /// </summary>
        public Guid? FlowId { get; set; }

        /// <summary>
        /// The matched AutoReply flow name, if available.
        /// </summary>
        public string? FlowName { get; set; }

        /// <summary>
        /// The keyword or rule that matched, for display/debugging.
        /// </summary>
        public string? MatchedKeyword { get; set; }

        /// <summary>
        /// The type of the Start node (e.g. "message", "template", etc.).
        /// </summary>
        public string? StartNodeType { get; set; }

        /// <summary>
        /// The name/label of the Start node.
        /// </summary>
        public string? StartNodeName { get; set; }
    }
}
