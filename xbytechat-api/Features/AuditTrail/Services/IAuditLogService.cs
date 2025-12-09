using xbytechat.api.Features.AuditTrail.Models;

namespace xbytechat.api.Features.AuditTrail.Services
{
    public interface IAuditLogService
    {
        Task SaveLogAsync(AuditLog log);
    }
}
