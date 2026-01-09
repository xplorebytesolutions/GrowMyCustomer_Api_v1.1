using System;
using System.Collections.Generic;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.ChatInbox.Models;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Models.BusinessModel;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.Features.BusinessModule.Models
{
    public class Business
    {
        public Guid Id { get; set; }

        // 🏢 Basic Info
        public string? CompanyName { get; set; }
        public string BusinessName { get; set; }
        public string BusinessEmail { get; set; }  // Not used for login, just business contact
        public string? RepresentativeName { get; set; }

        public Guid? CreatedByPartnerId { get; set; }
        public string? Phone { get; set; }
        public string? CompanyPhone { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }
        public string? Industry { get; set; }
        public string? LogoUrl { get; set; }

        // 📦 SaaS Plan & Status using Enums
        // public enum PlanType { Basic, Smart, Advanced } -- moved to bisinessinfo
        // public PlanType Plan { get; set; } = PlanType.Basic;  // moved to bisinessinfo
        public enum StatusType { Pending, Approved, Rejected, Hold }
        public StatusType Status { get; set; } = StatusType.Pending;  // Default to Pending

        // 📝 Metadata
        public string? Tags { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }

        // 📅 Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public bool IsApproved { get; set; } = false;
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // 🗑 Soft Deletion
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        // 👥 Navigation Property - List of Users (nullable if no users)
        public List<User> Users { get; set; } = new();


        public ICollection<MessageStatusLog> MessageStatusLogs { get; set; }
        public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
        // 🔗 Plan Info linked

        /// This is a one-to-one relationship with BusinessPlanInfo
        public BusinessPlanInfo? BusinessPlanInfo { get; set; }

        public Guid? PlanId { get; set; } // Nullable in case no plan is assigned yet
        public Plan? Plan { get; set; }   // Navigation property to the Plan entity

       // public WhatsAppSettingEntity WhatsAppSettings { get; set; }
        public ICollection<WhatsAppSettingEntity> WhatsAppSettings { get; set; }

        public ICollection<Role> Roles { get; set; }

        // 📥 Inbox visibility policy (Shared vs Restricted)
        public InboxVisibilityMode InboxVisibilityMode { get; set; } = InboxVisibilityMode.SharedInInbox;

    }
}
