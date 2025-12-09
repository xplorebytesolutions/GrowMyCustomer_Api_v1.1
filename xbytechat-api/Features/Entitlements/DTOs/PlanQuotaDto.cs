// 📄 Features/Entitlements/DTOs/PlanQuotaDto.cs
using System;
using xbytechat.api.Features.Entitlements.Models;

namespace xbytechat.api.Features.Entitlements.DTOs
{
    /// <summary>
    /// Admin-facing DTO for default quotas configured per plan.
    /// </summary>
    public sealed class PlanQuotaDto
    {
        public Guid Id { get; set; }

        public Guid PlanId { get; set; }

        // Canonical key, e.g. "MESSAGES_PER_MONTH"
        public string QuotaKey { get; set; } = string.Empty;

        // -1 => unlimited
        public long Limit { get; set; }

        public QuotaPeriod Period { get; set; }

        // Optional UX text used when quota is denied
        public string? DenialMessage { get; set; }
    }
}
