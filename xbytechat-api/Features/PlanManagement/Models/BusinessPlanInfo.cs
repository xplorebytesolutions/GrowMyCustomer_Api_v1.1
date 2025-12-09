using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.PlanManagement.Models;

namespace xbytechat.api.Models.BusinessModel
{
    public class BusinessPlanInfo
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🔗 Foreign key to Business
        [Required]
        public Guid BusinessId { get; set; }

        [ForeignKey(nameof(BusinessId))]
        public Business Business { get; set; }

        // 📦 Plan Management
        [Required]
        public PlanType Plan { get; set; } = PlanType.Trial; // Default Trial

        [Required]
        public int TotalMonthlyQuota { get; set; } = 100; // Default Trial Messages

        [Required]
        public int RemainingMessages { get; set; } = 100;

        public DateTime QuotaResetDate { get; set; } = DateTime.UtcNow.AddMonths(1);

        // 💰 Wallet Management (optional)
        public decimal WalletBalance { get; set; } = 0.00m;

        // 📅 Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
