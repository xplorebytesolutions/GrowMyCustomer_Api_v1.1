using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.AutoReplyBuilder.Models;
using xbytechat.api.Features.Inbox.Models;

namespace xbytechat.api.Features.Inbox.Services
{
    public class ChatSessionStateService : IChatSessionStateService
    {
        private readonly AppDbContext _db;

        public ChatSessionStateService(AppDbContext db)
        {
            _db = db;
        }

        // ✅ Returns current chat mode: "agent" or "auto"
        public async Task<string> GetChatModeAsync(Guid businessId, Guid contactId)
        {
            var session = await _db.ChatSessionStates
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.ContactId == contactId);

            return session?.Mode ?? "auto"; // Default to auto
        }

        // ✅ Switches to agent mode
        public async Task SwitchToAgentModeAsync(Guid businessId, Guid contactId)
        {
            await UpsertChatModeAsync(businessId, contactId, "agent");
        }

        // ✅ Switches to automation mode
        public async Task SwitchToAutomationModeAsync(Guid businessId, Guid contactId)
        {
            await UpsertChatModeAsync(businessId, contactId, "auto");
        }

        // ✅ Shared logic to insert or update session state
        private async Task UpsertChatModeAsync(Guid businessId, Guid contactId, string mode)
        {
            var existing = await _db.ChatSessionStates
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.ContactId == contactId);

            if (existing != null)
            {
                existing.Mode = mode;
                existing.LastUpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.ChatSessionStates.Add(new ChatSessionState
                {
                    BusinessId = businessId,
                    ContactId = contactId,
                    Mode = mode,
                    LastUpdatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
        }

        public async Task SetChatModeAsync(Guid businessId, Guid contactId, string mode)
        {
            var state = await _db.ChatSessionStates
                .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.ContactId == contactId);

            if (state == null)
            {
                // Insert new if not exists
                state = new ChatSessionState
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    ContactId = contactId,
                    Mode = mode,
                    LastUpdatedAt = DateTime.UtcNow
                };
                _db.ChatSessionStates.Add(state);
            }
            else
            {
                state.Mode = mode;
                state.LastUpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }
    }
}
