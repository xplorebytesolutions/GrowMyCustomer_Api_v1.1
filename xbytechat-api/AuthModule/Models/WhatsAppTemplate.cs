using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.AuthModule.Models
{
    // All enum usages removed. We store canonical strings instead.

    //[Index(nameof(BusinessId), nameof(Provider))]
    //[Index(nameof(BusinessId), nameof(Name))]
    //[Index(nameof(BusinessId), nameof(Name), nameof(LanguageCode), nameof(Provider), IsUnique = true)]
    public sealed class WhatsAppTemplate
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }

        // Provider is stored as UPPERCASE per your decision: e.g., "META_CLOUD", "PINNACLE", "OTHER"
        [MaxLength(40)]
        public string Provider { get; set; } = "META_CLOUD";

        public string? TemplateId { get; set; }

        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(16)]
        public string LanguageCode { get; set; } = "en_US";

        // Former TemplateStatus enum → string (canonical UPPERCASE: APPROVED/REJECTED/PENDING/PAUSED/UNKNOWN)
        [MaxLength(24)]
        public string Status { get; set; } = "APPROVED";

        public string? Category { get; set; }
        public string? SubCategory { get; set; }

        // Full provider JSON
        public string RawJson { get; set; } = "{}";

        // Former ParameterFormat enum → string (canonical UPPERCASE: POSITIONAL/NAMED/UNKNOWN)
        [MaxLength(24)]
        public string ParameterFormat { get; set; } = "UNKNOWN";

        // Former HeaderKind enum → string (canonical lowercase: none/text/image/video/document/location)
        [MaxLength(16)]
        public string HeaderKind { get; set; } = "none";

        public string? HeaderText { get; set; }
        public string? Body { get; set; }
        public string? BodyPreview { get; set; }

        public bool RequiresMediaHeader { get; set; }

        // Counts (kept as ints)
        public int BodyVarCount { get; set; }
        public int HeaderTextVarCount { get; set; }
        public int TotalTextParamCount { get; set; }

        // JSON blobs
        public string? UrlButtons { get; set; }      // jsonb
        public int QuickReplyCount { get; set; }
        public bool HasPhoneButton { get; set; }
        public string? NamedParamKeys { get; set; }  // jsonb
        public string? PlaceholderMap { get; set; }  // jsonb

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

        // Concurrency
        public byte[]? RowVersion { get; set; }
    }
}


//using Microsoft.EntityFrameworkCore;
//using System.ComponentModel.DataAnnotations;

//namespace xbytechat.api.AuthModule.Models
//{
//    [Index(nameof(BusinessId), nameof(Provider))]
//    [Index(nameof(BusinessId), nameof(Name))]
//    [Index(nameof(BusinessId), nameof(Name), nameof(Language), nameof(Provider), IsUnique = true)]
//    // xbytechat_api/WhatsAppSettings/Models/WhatsAppTemplate.cs
//    public sealed class WhatsAppTemplate
//    {
//        public Guid Id { get; set; }
//        public Guid BusinessId { get; set; }
//        public string Provider { get; set; } = "META_CLOUD";
//        public string? ExternalId { get; set; }
//        public string Name { get; set; } = "";
//        public string? Language { get; set; }
//        public string Status { get; set; } = "APPROVED";
//        public string? Category { get; set; }

//        // Already present in your code:
//        public string? HeaderKind { get; set; }           // "text", "image", "video", "document", "none"
//        public string? Body { get; set; }                 // we keep this as the COMBINED PREVIEW (headerText + bodyText)
//        public int PlaceholderCount { get; set; }
//        public string? ButtonsJson { get; set; }
//        public string? RawJson { get; set; }

//        // ✅ NEW: cached, de-normalized fields for debug + send-time mapping
//        public string? ParameterFormat { get; set; }      // "POSITIONAL" | "NAMED" | "MIXED" | "UNKNOWN"
//        public string? HeaderText { get; set; }           // only for text headers; null for media headers
//        public string? BodyText { get; set; }             // body block text as-is (no header included)

//        // token metadata (JSON for flexibility)
//        public string? HeaderParamIndicesJson { get; set; }   // e.g. "[1,2]"
//        public string? BodyParamIndicesJson { get; set; }     // e.g. "[1,3]"
//        public string? ButtonParamTemplatesJson { get; set; } // e.g. "[{ order:1, type:'URL', text:'..', paramTemplate:'..', paramIndices:[1]}]"
//        public string? PlaceholderOccurrencesJson { get; set; } // full `Placeholders` array from helper (optional)

//        public bool IsActive { get; set; } = true;
//        public DateTime CreatedAt { get; set; }
//        public DateTime UpdatedAt { get; set; }
//        public DateTime LastSyncedAt { get; set; }
//    }

//}
