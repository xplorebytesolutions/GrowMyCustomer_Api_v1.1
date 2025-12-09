namespace xbytechat.api.Features.Webhooks.Services
{
    public interface IMaintenanceService
    {
        Task<bool> IsAutoCleanupEnabledAsync();
        Task EnableAutoCleanupAsync();
        Task DisableAutoCleanupAsync();
        Task<DateTime?> GetLastCleanupTimeAsync();
        Task<int> RunCleanupAsync();
    }
}
