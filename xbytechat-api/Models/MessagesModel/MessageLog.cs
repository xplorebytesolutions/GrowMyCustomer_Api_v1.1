using System;
using System.Collections.Generic;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.MessageManagement.DTOs;

public class MessageLog
{
    public Guid Id { get; set; }

    public string? MessageId { get; set; } // WAMID from WhatsApp — alternate key
                                           // public ICollection<MessageStatusLog> StatusUpdates { get; set; } = new List<MessageStatusLog>();
    public Guid? RunId { get; set; }
    // 🔗 FK to Business
    public Guid BusinessId { get; set; }
    public Business Business { get; set; }

    // 📨 Message Info
    public string RecipientNumber { get; set; }
    public string MessageContent { get; set; }
    public string? MediaUrl { get; set; }

    // ?? Inbox media (WhatsApp native media storage; no public URLs)
    public string? MediaId { get; set; }        // WhatsApp Cloud API media_id
    public string? MediaType { get; set; }      // "text" | "image" | "document" | "video" | "audio" | "location"
    public string? FileName { get; set; }       // original filename (for UI display)
    public string? MimeType { get; set; }       // e.g. "image/jpeg", "application/pdf"

    // 🧾 Status Info
    // ?? Location payload (WhatsApp "location" messages)
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }

    public string Status { get; set; } = "Queued";
    public string? ErrorMessage { get; set; }
    public string? RawResponse { get; set; }

    // 🕒 Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    // 🔗 Contact (optional)
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }

    // 🔗 Campaign (optional)
    public Guid? CampaignId { get; set; }

    // 🔗 CTA Flow Tracking
    public Guid? CTAFlowConfigId { get; set; }  // Which visual flow config this message belongs to
    public Guid? CTAFlowStepId { get; set; }    // Which flow step (template) this message originated from

    public int? FlowVersion { get; set; }                // which version of the flow this message belongs to
    public string? ButtonBundleJson { get; set; }

    public Campaign? SourceCampaign { get; set; } // renamed from "Campaign" to avoid name conflict

    public bool IsIncoming { get; set; }

    public string? RenderedBody { get; set; } // actual resolved message with parameters

    public Guid? RefMessageId { get; set; }
    public string? Source { get; set; } // e.g., "campaign", "flow", "manual"


    public string? Provider { get; set; }                     // "Meta_cloud", "Pinnacle", etc.
    public string? ProviderMessageId { get; set; }            // e.g., "wamid.HBg..."; indexed
    public bool? IsChargeable { get; set; }                 // true/false/unknown
    public string? ConversationId { get; set; }               // provider conv id (Meta)
    public string? ConversationCategory { get; set; }         // "marketing" | "utility" | "authentication" | "service" | "free_entry" | "unknown"
    public DateTimeOffset? ConversationStartedAt { get; set; }
    public decimal? PriceAmount { get; set; }                 // nullable until known
    public string? PriceCurrency { get; set; }     
    // "USD", "INR", etc.
}
