using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Result of a runtime match attempt.
    /// </summary>
    public sealed class AutoReplyMatchResultDto
    {
        public Guid? FlowId { get; set; }
        public string? FlowName { get; set; }
        public string? MatchedKeyword { get; set; }
        public string? MatchType { get; set; }
        public Guid? StartNodeId { get; set; }
        public string? StartNodeType { get; set; }
        public string? StartNodeConfigJson { get; set; }
        public bool IsMatch { get; set; }
    }
}
