using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.BusinessModule.Models; // 🆕 Required for navigation

namespace xbytechat.api.AuthModule.Models
{
    public class User
    {
        public Guid Id { get; set; }

        // 🔗 FK to Business
        public Guid? BusinessId { get; set; }
        public Business Business { get; set; }

        // 👤 User Info
        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        // 🛡️ Role System
        // 🛡️ Role System (FK + Navigation)
        public Guid? RoleId { get; set; }
        public Role Role { get; set; }// admin / business / agent / staff

        // ✅ Status Management
        public string Status { get; set; } = "Pending"; // Active / Hold / Rejected / Pending

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🗑️ Soft Delete Role 
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public List<CampaignSendLog> SendLogs { get; set; }
        public ICollection<MessageStatusLog> MessageStatusLogs { get; set; }

        // 🆕 Permission Navigation
        public ICollection<UserPermission> UserPermissions { get; set; } // 💡 Enables .WithMany(u => u.UserPermissions)
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

    }
}
