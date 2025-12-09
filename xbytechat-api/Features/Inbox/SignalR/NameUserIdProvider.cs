using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace xbytechat.api.SignalR
{
    public class NameUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
