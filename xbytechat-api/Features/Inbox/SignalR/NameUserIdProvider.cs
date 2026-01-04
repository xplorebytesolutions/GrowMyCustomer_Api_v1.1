using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace xbytechat.api.SignalR
{
    public sealed class NameUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            var user = connection.User;
            if (user == null) return null;

            // Prefer NameIdentifier
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(id)) return id;

            // Fallbacks (depending on how JWT was created)
            id = user.FindFirstValue("sub");
            if (!string.IsNullOrWhiteSpace(id)) return id;

            id = user.FindFirstValue("userId");
            if (!string.IsNullOrWhiteSpace(id)) return id;

            return null;
        }
    }
}
