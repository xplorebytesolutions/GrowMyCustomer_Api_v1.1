using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.ESU.Facebook.Models;
using xbytechat.api.Infrastructure;

public sealed class EsuTokenStore : IEsuTokenStore
{
    private readonly AppDbContext _db;
    public EsuTokenStore(AppDbContext db) => _db = db;

    public Task<EsuToken?> GetAsync(Guid biz, string provider, CancellationToken ct)
        => _db.Set<EsuToken>().AsNoTracking()
              .FirstOrDefaultAsync(x => x.BusinessId == biz && x.Provider == provider, ct);

    public async Task UpsertAsync(Guid biz, string provider, string token, DateTime? exp, CancellationToken ct)
    {
        provider = provider.ToUpperInvariant();

        var row = await _db.Set<EsuToken>()
            .FirstOrDefaultAsync(x => x.BusinessId == biz && x.Provider == provider, ct);

        if (row is null)
        {
            _db.Add(new EsuToken
            {
                BusinessId = biz,
                Provider = provider,
                AccessToken = token,
                ExpiresAtUtc = exp,
                IsRevoked = string.IsNullOrWhiteSpace(token) // treat empty as revoked
            });
        }
        else
        {
            row.AccessToken = token;
            row.ExpiresAtUtc = exp;
            row.IsRevoked = string.IsNullOrWhiteSpace(token) ? true : false;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }


    public async Task RevokeAsync(Guid biz, string provider, CancellationToken ct)
    {
        var row = await _db.Set<EsuToken>().FirstOrDefaultAsync(x => x.BusinessId == biz && x.Provider == provider, ct);
        if (row is null) return;
        row.IsRevoked = true; row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // FILE: Features/ESU/Facebook/Services/EsuTokenStore.cs

    public async Task DeleteAsync(Guid biz, string provider, CancellationToken ct = default)
    {
        var set = _db.Set<EsuToken>();

        var rows = await set
            .Where(x => x.BusinessId == biz && x.Provider == provider)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return;

        set.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
    }

}
