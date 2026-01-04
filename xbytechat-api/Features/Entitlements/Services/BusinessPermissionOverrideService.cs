using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Entitlements.DTOs;
using xbytechat.api.Features.Entitlements.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Entitlements.Services
{
    public sealed class BusinessPermissionOverrideService : IBusinessPermissionOverrideService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<BusinessPermissionOverrideService> _logger;

        public BusinessPermissionOverrideService(AppDbContext db, ILogger<BusinessPermissionOverrideService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<BusinessPermissionOverrideDto>> GetAsync(Guid businessId)
        {
            var now = DateTime.UtcNow;

            return await _db.BusinessPermissionOverrides
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && !x.IsRevoked)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Select(x => new BusinessPermissionOverrideDto
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    PermissionCode = x.Permission!.Code,
                    IsGranted = x.IsGranted,
                    IsRevoked = x.IsRevoked,
                    Reason = x.Reason,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    CreatedAtUtc = x.CreatedAtUtc,
                    UpdatedAtUtc = x.UpdatedAtUtc
                })
                .ToListAsync();
        }

        public async Task<ResponseResult> UpsertAsync(Guid businessId, Guid actorUserId, UpsertBusinessPermissionOverrideDto dto)
        {
            var code = (dto.PermissionCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
                return ResponseResult.ErrorInfo("❌ PermissionCode is required.");

            var perm = await _db.Permissions.FirstOrDefaultAsync(p => p.IsActive && p.Code == code.ToUpper());
            if (perm == null)
                return ResponseResult.ErrorInfo("❌ Invalid permission code.");

            var row = await _db.BusinessPermissionOverrides
                .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.PermissionId == perm.Id);

            if (row == null)
            {
                row = new BusinessPermissionOverride
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    PermissionId = perm.Id,
                    IsGranted = dto.IsGranted,
                    IsRevoked = false,
                    Reason = dto.Reason?.Trim(),
                    ExpiresAtUtc = dto.ExpiresAtUtc,
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _db.BusinessPermissionOverrides.Add(row);
            }
            else
            {
                row.IsGranted = dto.IsGranted;
                row.IsRevoked = false;
                row.Reason = dto.Reason?.Trim();
                row.ExpiresAtUtc = dto.ExpiresAtUtc;
                row.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("✅ Business permission override upserted. BusinessId={BusinessId}, Permission={Permission}, Granted={Granted}, ActorUserId={ActorUserId}",
                businessId, perm.Code, dto.IsGranted, actorUserId);

            return ResponseResult.SuccessInfo("✅ Override saved.");
        }

        public async Task<ResponseResult> RevokeByPermissionCodeAsync(Guid businessId, Guid actorUserId, string permissionCode)
        {
            var code = (permissionCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
                return ResponseResult.ErrorInfo("❌ PermissionCode is required.");

            var perm = await _db.Permissions.FirstOrDefaultAsync(p => p.IsActive && p.Code == code.ToUpper());
            if (perm == null)
                return ResponseResult.ErrorInfo("❌ Invalid permission code.");

            var row = await _db.BusinessPermissionOverrides
                .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.PermissionId == perm.Id && !x.IsRevoked);

            if (row == null)
                return ResponseResult.ErrorInfo("❌ Override not found.");

            row.IsRevoked = true;
            row.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("✅ Business permission override revoked. BusinessId={BusinessId}, Permission={Permission}, ActorUserId={ActorUserId}",
                businessId, perm.Code, actorUserId);

            return ResponseResult.SuccessInfo("✅ Override revoked.");
        }
    }
}
