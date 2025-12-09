using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.ESU.Facebook.Models; // IntegrationFlags

namespace xbytechat.api.Features.ESU.Shared.Infrastructure
{
    public sealed class UpdatedAtUtcInterceptor : SaveChangesInterceptor
    {
        private static readonly Type[] _trackedTypes = new[]
        {
            typeof(IntegrationFlags),
            // add other entities if you want auto-bump later
        };

        private static bool ShouldTrack(object entity)
            => _trackedTypes.Contains(entity.GetType());

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            Touch(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Touch(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void Touch(DbContext? ctx)
        {
            if (ctx == null) return;

            var now = DateTime.UtcNow;

            foreach (var entry in ctx.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Modified && ShouldTrack(entry.Entity))
                {
                    var prop = entry.Property("UpdatedAtUtc");
                    if (prop != null) prop.CurrentValue = now;
                }
            }
        }
    }
}
