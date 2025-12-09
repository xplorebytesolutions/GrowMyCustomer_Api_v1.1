using System;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// DTO for a single node inside an auto-reply flow.
    /// Maps directly to <see cref="Models.AutoReplyFlowNode"/> so builder edits round-trip cleanly.
    /// </summary>
    public sealed class AutoReplyNodeDto
    {
        public Guid? Id { get; set; }
        public string NodeType { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? NodeName { get; set; }
        public string? ConfigJson { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public int Order { get; set; }
    }
}
