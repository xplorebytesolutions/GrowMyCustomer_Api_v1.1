#nullable enable
using System;
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

        //public async Task<EsuStatusDto> GetStatusAsync(Guid businessId, CancellationToken ct = default)
        //{
        //    if (businessId == Guid.Empty)
        //        throw new ArgumentException("businessId is required.", nameof(businessId));

        //    var row = await _db.IntegrationFlags
        //        .AsNoTracking()
        //        .SingleOrDefaultAsync(x => x.BusinessId == businessId, ct);

        //    if (row is null)
        //    {
        //        return new EsuStatusDto
        //        {
        //            Connected = false,
        //            TokenExpiresAtUtc = null,
        //            WillExpireSoon = false,
        //            UpdatedAtUtc = DateTime.UtcNow,
        //            Debug = "no-row"
        //        };
        //    }

        //    DateTime? expiresAt = null;

        //    bool willExpireSoon = false;
        //    try
        //    {
        //        var tok = await _tokenService.TryGetValidAsync(businessId, ct);
        //        if (tok is not null)
        //        {
        //            expiresAt = tok.ExpiresAtUtc;
        //            willExpireSoon = tok.WillExpireSoon();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _log.LogDebug(ex, "Status check token probe failed for {Biz}", businessId);
        //    }

        //    return new EsuStatusDto
        //    {
        //        Connected = row.FacebookEsuCompleted && expiresAt.HasValue && !willExpireSoon,
        //        HasEsuFlag = row.FacebookEsuCompleted,
        //        HasValidToken = expiresAt.HasValue && !willExpireSoon,
        //        TokenExpiresAtUtc = expiresAt,
        //        WillExpireSoon = willExpireSoon,
        //        UpdatedAtUtc = row.UpdatedAtUtc,
        //        Debug = null
        //    };
        //}

        public async Task<EsuStatusDto> GetStatusAsync(Guid businessId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("businessId is required.", nameof(businessId));

            var row = await _db.IntegrationFlags
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.BusinessId == businessId, ct);

            if (row is null)
            {
                return new EsuStatusDto
                {
                    Connected = false,
                    HasEsuFlag = false,
                    HasValidToken = false,
                    TokenExpiresAtUtc = null,
                    WillExpireSoon = false,
                    UpdatedAtUtc = DateTime.UtcNow,
                    Debug = "no-row"
                };
            }

            DateTime? expiresAt = null;
            bool hasValidToken = false;
            bool willExpireSoon = false;

            try
            {
                // TryGetValidAsync already enforces "not expired" and "not expiring soon" for sending.
                var tok = await _tokenService.TryGetValidAsync(businessId, ct);
                if (tok is not null)
                {
                    expiresAt = tok.ExpiresAtUtc;
                    // In current implementation this will always be false,
                    // but we keep it for future-proofing.
                    willExpireSoon = tok.WillExpireSoon();
                    hasValidToken = true;
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Status check token probe failed for {Biz}", businessId);
            }

            return new EsuStatusDto
            {
                Connected = row.FacebookEsuCompleted && hasValidToken,
                HasEsuFlag = row.FacebookEsuCompleted,
                HasValidToken = hasValidToken,
                TokenExpiresAtUtc = expiresAt,      // may be null (Meta didn’t send expiry)
                WillExpireSoon = willExpireSoon,  // currently always false, but kept for contract
                UpdatedAtUtc = row.UpdatedAtUtc,
                Debug = null
            };
        }

        public async Task DeauthorizeAsync(Guid businessId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("businessId is required.", nameof(businessId));

            // 1) Clear UX flag
            var row = await _db.IntegrationFlags.SingleOrDefaultAsync(x => x.BusinessId == businessId, ct);
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
