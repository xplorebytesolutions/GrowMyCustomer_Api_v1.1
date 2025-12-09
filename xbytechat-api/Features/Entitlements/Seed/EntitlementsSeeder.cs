using System;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.Entitlements.Models;
using Microsoft.EntityFrameworkCore;

namespace xbytechat.api.Features.Entitlements.Seed
{
    public static class EntitlementsSeeder
    {
        public static async Task SeedAsync(AppDbContext db, Guid planId)
        {
            var defaults = new[]
            {
                new PlanQuota { Id = Guid.NewGuid(), PlanId = planId, QuotaKey = "MESSAGES_PER_MONTH", Limit = 10000, Period = QuotaPeriod.Monthly, DenialMessage = "Monthly message limit reached." },
                new PlanQuota { Id = Guid.NewGuid(), PlanId = planId, QuotaKey = "CAMPAIGNS_PER_DAY",   Limit = 10,    Period = QuotaPeriod.Daily,   DenialMessage = "Daily campaign limit reached." },
                new PlanQuota { Id = Guid.NewGuid(), PlanId = planId, QuotaKey = "TEMPLATES_TOTAL",     Limit = -1,    Period = QuotaPeriod.Lifetime } // unlimited
            };

            foreach (var q in defaults)
            {
                var exists = await db.PlanQuotas.AnyAsync(p => p.PlanId == planId && p.QuotaKey.ToUpper() == q.QuotaKey.ToUpper());
                if (!exists) db.PlanQuotas.Add(q);
            }
            await db.SaveChangesAsync();
        }
    }
}
