using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Webhooks.Models;

namespace xbytechat.api.Features.Webhooks.Services
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly AppDbContext _context;

        public MaintenanceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsAutoCleanupEnabledAsync()
        {
            var setting = await _context.WebhookSettings
                .AsNoTracking()
                .FirstOrDefaultAsync();

            return setting?.AutoCleanupEnabled ?? false;
        }

        public async Task<DateTime?> GetLastCleanupTimeAsync()
        {
            return await _context.WebhookSettings
                .AsNoTracking()
                .Select(s => s.LastCleanupAt)
                .FirstOrDefaultAsync();
        }

        public async Task EnableAutoCleanupAsync()
        {
            var setting = await GetOrCreateAsync();
            setting.AutoCleanupEnabled = true;
            await _context.SaveChangesAsync();
        }

        public async Task DisableAutoCleanupAsync()
        {
            var setting = await GetOrCreateAsync();
            setting.AutoCleanupEnabled = false;
            await _context.SaveChangesAsync();
        }

        public async Task<int> RunCleanupAsync()
        {
            var threshold = DateTime.UtcNow.AddDays(-7);
            var oldLogs = await _context.FailedWebhookLogs
                .Where(l => l.CreatedAt < threshold)
                .ToListAsync();

            if (oldLogs.Any())
                _context.FailedWebhookLogs.RemoveRange(oldLogs);

            var setting = await GetOrCreateAsync();
            setting.LastCleanupAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return oldLogs.Count;
        }

        private async Task<WebhookSettings> GetOrCreateAsync()
        {
            var setting = await _context.WebhookSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new WebhookSettings
                {
                    AutoCleanupEnabled = false,
                    LastCleanupAt = null
                };
                _context.WebhookSettings.Add(setting);
                await _context.SaveChangesAsync();
            }
            return setting;
        }
    }
}
