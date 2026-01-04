using System;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Models.BusinessModel;

namespace xbytechat.api.Features.Entitlements.Models
{
    /// <summary>
    /// Business-level permission override.
    /// Used ONLY by internal admins (SuperAdmin/Partner/Reseller) to grant/deny permissions
    /// beyond plan defaults (VIP, pilots, temporary unlocks).
    ///
    /// NOTE:
    /// - This overrides PLAN-level availability for a business.
    /// - It still should NOT bypass ROLE limitations when computing "effective permissions"
    ///   for a staff user (we will enforce that in the entitlement calculation step).
    /// </summary>
    public sealed class BusinessPermissionOverride
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid BusinessId { get; set; }
        public Business? Business { get; set; }

        public Guid PermissionId { get; set; }
        public Permission? Permission { get; set; }

        /// <summary>
        /// true = grant, false = deny.
        /// </summary>
        public bool IsGranted { get; set; }

        /// <summary>
        /// Soft revoke / disable the override.
        /// </summary>
        public bool IsRevoked { get; set; }

        /// <summary>
        /// Optional reason for auditability (VIP deal, pilot, migration, support).
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Optional expiry for temporary unlocks.
        /// If set and expired, override should be ignored by entitlement computation.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; }

        /// <summary>
        /// Who applied this override (admin user id).
        /// </summary>
        public Guid? CreatedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
