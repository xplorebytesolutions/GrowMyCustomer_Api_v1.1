namespace xbytechat.api.Features.Inbox.Services
{
    public interface IChatSessionStateService
    {
        Task<string> GetChatModeAsync(Guid businessId, Guid contactId);
        Task SwitchToAgentModeAsync(Guid businessId, Guid contactId);
        Task SwitchToAutomationModeAsync(Guid businessId, Guid contactId);
        Task SetChatModeAsync(Guid businessId, Guid contactId, string mode);
    }
}
