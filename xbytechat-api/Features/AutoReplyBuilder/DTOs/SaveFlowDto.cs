using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.AutoReplyBuilder.Flows.DTOs
{
    public class SaveFlowDto
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public string Name { get; set; }
        public string? TriggerKeyword { get; set; }
        public List<NodeDto> Nodes { get; set; } = new();
        public List<EdgeDto> Edges { get; set; } = new();
        public DateTime CreatedAt { get; set; }

        public string? IndustryTag { get; set; }     // e.g. "restaurant", "clinic", etc.
        public string? UseCase { get; set; }         // e.g. "Order Flow", "Appointment Flow"
        public bool IsDefaultTemplate { get; set; } = false; // Flag for prebuilt templates

    }

    public class NodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public AutoPositionDto Position { get; set; } = new();
        public NodeDataDto Data { get; set; } = new();
    }

    public class AutoPositionDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class NodeDataDto
    {
        public string Label { get; set; } = string.Empty;
        public object Config { get; set; } = new { };
    }

    public class EdgeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string SourceHandle { get; set; } = string.Empty;
        public string TargetHandle { get; set; } = string.Empty;
    }
}


//using System;

//namespace xbytechat.api.Features.AutoReplyBuilder.Flows.DTOs
//{
//    public class SaveFlowDto
//    {
//        public Guid Id { get; set; }
//        public Guid BusinessId { get; set; }
//        public string Name { get; set; } = string.Empty;
//        public List<Dictionary<string, object>> Nodes { get; set; }

//        public List<Dictionary<string, object>> Edges { get; set; }
//        public DateTime CreatedAt { get; set; }
//        public string? TriggerKeyword { get; set; }

//    }
//}
