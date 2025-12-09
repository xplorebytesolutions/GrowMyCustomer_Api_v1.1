using xbytechat.api.Features.AuditTrail.Models;
using xbytechat.api.Repositories;
using xbytechat.api.Repositories.Interfaces;

namespace xbytechat.api.Features.AuditTrail.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IGenericRepository<AuditLog> _repo;

        public AuditLogService(IGenericRepository<AuditLog> repo)
        {
            _repo = repo;
        }

        public async Task SaveLogAsync(AuditLog log)
        {
            await _repo.AddAsync(log);
        }
    }
}
