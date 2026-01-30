#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.DTOs;
using xbytechat.api.Infrastructure;

namespace xbytechat.api.Features.ESU.Facebook.Services
{
    public sealed class EsuStatusService : IEsuStatusService
    {
        private const string Provider = "META_CLOUD";

        private readonly AppDbContext _db;
        private readonly IFacebookTokenService _tokenService;
        private readonly IEsuTokenStore _tokens;
        private readonly ILogger<EsuStatusService> _log;

        public EsuStatusService(
            AppDbContext db,
            IFacebookTokenService tokenService,
            IEsuTokenStore tokens,
            ILogger<EsuStatusService> log)
        {
            _db = db;
            _tokenService = tokenService;
            _tokens = tokens;
            _log = log;
        }

        public async Task<EsuStatusDto> GetStatusAsync(Guid businessId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("businessId is required.", nameof(businessId));

            var now = DateTime.UtcNow;

            // 1) Read IntegrationFlags row (UX flag)
            var row = await _db.IntegrationFlags
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.BusinessId == businessId, ct);

            var hasEsuFlag = row?.FacebookEsuCompleted ?? false;

            // 2) Pull latest token row (even if expiring/expired) to show expiry in UI
            //    (TryGetValidAsync hides expiring tokens by design)
            var latestToken = await _db.EsuTokens
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.Provider == Provider)
                .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            DateTime? expiresAt = latestToken?.ExpiresAtUtc;

            // 3) Determine "valid token" using canonical service logic
            bool hasValidToken = false;
            try
            {
                var valid = await _tokenService.TryGetValidAsync(businessId, ct);
                hasValidToken = valid is not null;

                // Prefer valid token expiry if token row doesn't have it for some reason
                expiresAt ??= valid?.ExpiresAtUtc;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Status check token probe failed for {Biz}", businessId);
            }

            // 4) HardDeleted detection (your intended terminal state)
            var hasAnyToken = await _db.EsuTokens
                .AsNoTracking()
                .AnyAsync(x => x.BusinessId == businessId && x.Provider == Provider, ct);

            var hasAnySetting = await _db.WhatsAppSettings
                .AsNoTracking()
                .AnyAsync(x => x.BusinessId == businessId && x.Provider == Provider, ct);

            var hasAnyPhone = await _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .AnyAsync(x => x.BusinessId == businessId && x.Provider == Provider, ct);

            // ✅ Terminal state: no flags row + no tokens/settings/phones
            var hardDeleted = (row is null) && !hasAnyToken && !hasAnySetting && !hasAnyPhone;

            // 5) Expiring soon (UI signal)
            var willExpireSoon =
                expiresAt.HasValue &&
                expiresAt.Value > now &&
                expiresAt.Value <= now.AddMinutes(10);

            var updatedAt = row?.UpdatedAtUtc ?? now;

            return new EsuStatusDto
            {
                Connected = hasEsuFlag && hasValidToken,
                HasEsuFlag = hasEsuFlag,
                HasValidToken = hasValidToken,
                TokenExpiresAtUtc = expiresAt,
                WillExpireSoon = willExpireSoon,
                HardDeleted = hardDeleted,
                UpdatedAtUtc = updatedAt,
                Debug = row is null ? "no-row" : null
            };
        }

        public async Task DeauthorizeAsync(Guid businessId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("businessId is required.", nameof(businessId));

            // 1) Clear UX flag
            var row = await _db.IntegrationFlags
                .SingleOrDefaultAsync(x => x.BusinessId == businessId, ct);

            if (row is not null)
            {
                row.FacebookEsuCompleted = false;
                row.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            // 2) Revoke token (EsuTokens) + 3) drop from cache
            await _tokens.RevokeAsync(businessId, Provider, ct);
            await _tokenService.InvalidateAsync(businessId, ct);
        }
    }
}
