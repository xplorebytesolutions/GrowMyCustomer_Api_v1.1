using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
{
    /// <summary>
    /// Full flow DTO (including nodes) for CRUD in the builder.
    /// </summary>
    public sealed class AutoReplyFlowDto
    {
        public Guid? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }

        /// <summary>
        /// Comma / newline separated trigger keywords, e.g. "hi, hello".
        /// </summary>
        public string? TriggerKeyword { get; set; }

        public string? IndustryTag { get; set; }

        public string? UseCase { get; set; }

        public bool IsDefaultTemplate { get; set; }

        /// <summary>
        /// Matching mode for this flow:
        /// "Exact" | "Word" | "StartsWith" | "Contains".
        /// Defaults to "Word" on the backend when not provided.
        /// </summary>
        public string MatchMode { get; set; } = "Word";

        /// <summary>
        /// Priority for choosing between multiple matching flows.
        /// Higher values win. Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// When the flow was first created (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the flow was last updated (UTC). Null if never updated after creation.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        public List<AutoReplyNodeDto> Nodes { get; set; } = new();
    }
}


//using System;
//using System.Collections.Generic;

//namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
//{
//    /// <summary>
//    /// Full flow DTO (including nodes) for CRUD in the builder.
//    /// </summary>
//    public sealed class AutoReplyFlowDto
//    {
//        public Guid? Id { get; set; }

//        public string Name { get; set; } = string.Empty;

//        public string? Description { get; set; }

//        public bool IsActive { get; set; }

//        public string? TriggerKeyword { get; set; }

//        public string? IndustryTag { get; set; }

//        public string? UseCase { get; set; }

//        public bool IsDefaultTemplate { get; set; }

//        /// <summary>
//        /// When the flow was first created (UTC).
//        /// </summary>
//        public DateTime CreatedAt { get; set; }

//        /// <summary>
//        /// When the flow was last updated (UTC). Null if never updated after creation.
//        /// </summary>
//        public DateTime? UpdatedAt { get; set; }

//        public List<AutoReplyNodeDto> Nodes { get; set; } = new();
//    }
//}


//using System;
//using System.Collections.Generic;

//namespace xbytechat.api.Features.AutoReplyBuilder.DTOs
//{
//    /// <summary>
//    /// Full flow DTO (including nodes) for CRUD in the builder.
//    /// </summary>
//    public sealed class AutoReplyFlowDto
//    {
//        public Guid? Id { get; set; }
//        public string Name { get; set; } = string.Empty;
//        public string? Description { get; set; }
//        public bool IsActive { get; set; }
//        public string? TriggerKeyword { get; set; }
//        public string? IndustryTag { get; set; }
//        public string? UseCase { get; set; }
//        public bool IsDefaultTemplate { get; set; }
//        public List<AutoReplyNodeDto> Nodes { get; set; } = new();
//    }
//}
