using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Runtime shape for an AutoReply flow definition, deserialized from AutoReplyFlow.NodesJson.
    /// This is NOT an EF entity and is never mapped to a DB table.
    /// </summary>
    public sealed class AutoReplyFlowDefinitionDto
    {
        /// <summary>
        /// Optional – if your NodesJson stores the starting node id.
        /// If your runtime method uses this, it will be populated; otherwise it can stay null/empty.
        /// </summary>
        public string? StartNodeId { get; set; }

        /// <summary>
        /// All nodes that belong to this flow, in whatever order they were saved by the builder.
        /// ExecuteFlowLinearAsync can order or chain them as it likes.
        /// </summary>
        public List<AutoReplyFlowNodeDto> Nodes { get; set; } = new();
    }

    /// <summary>
    /// Runtime shape for a single node inside an AutoReply flow.
    /// This is intentionally kept as a "flattened" shape that matches the
    /// needs of ExecuteFlowLinearAsync:
    /// - Node type (message / template / set-tag / wait)
    /// - Content fields for that type
    /// - Linear chaining info (Order / NextNodeId)
    /// </summary>
    public sealed class AutoReplyFlowNodeDto
    {
        /// <summary>
        /// Unique id of the node inside this flow (often the same as the ReactFlow node id).
        /// </summary>
        public string Id { get; set; } = null!;

        /// <summary>
        /// Node type. Expected values (by the new runtime) include:
        /// "message", "template", "set-tag", "wait"
        /// </summary>
        public string Type { get; set; } = null!;

        /// <summary>
        /// Optional friendly name / label shown in the builder UI.
        /// Not required for runtime, but useful for logs and debugging.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Execution order for linear flows, if you are ordering by an explicit number.
        /// If your runtime doesn’t use this, it can safely remain 0.
        /// </summary>
        public int Order { get; set; }

        // ----- Content for "message" nodes -----

        /// <summary>
        /// Plain text to send for "message" type nodes.
        /// </summary>
        public string? Text { get; set; }

        // ----- Content for "template" nodes -----

        /// <summary>
        /// Template name for "template" type nodes.
        /// </summary>
        public string? TemplateName { get; set; }

        /// <summary>
        /// Optional template namespace / category if you store it.
        /// </summary>
        public string? TemplateNamespace { get; set; }

        /// <summary>
        /// Optional template language code (e.g., "en", "en_US").
        /// </summary>
        public string? TemplateLanguage { get; set; }

        // ----- Content for "set-tag" nodes -----

        /// <summary>
        /// Tag key to set on the contact (e.g., "lead_stage").
        /// </summary>
        public string? TagKey { get; set; }

        /// <summary>
        /// Tag value to set on the contact (e.g., "hot", "warm", "cold").
        /// </summary>
        public string? TagValue { get; set; }

        // ----- Content for "wait" nodes -----

        /// <summary>
        /// Delay for "wait" nodes, in seconds.
        /// </summary>
        public int? WaitSeconds { get; set; }

        // ----- Basic chaining -----

        /// <summary>
        /// Next node id for simple linear flows.
        /// If your runtime walks the flow by following NextNodeId,
        /// this will be populated when saving from the builder.
        /// </summary>
        public string? NextNodeId { get; set; }
    }
}
