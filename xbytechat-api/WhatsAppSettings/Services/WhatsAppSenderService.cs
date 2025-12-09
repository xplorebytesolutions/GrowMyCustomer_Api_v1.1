using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.WhatsAppSettings.DTOs;               
namespace xbytechat.api.WhatsAppSettings.Services
{
    public sealed class WhatsAppSenderService : IWhatsAppSenderService
    {
        private readonly AppDbContext _db;
        public WhatsAppSenderService(AppDbContext db) => _db = db;

        public async Task<IReadOnlyList<WhatsAppSenderDto>> GetBusinessSendersAsync(
            Guid businessId,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) return Array.Empty<WhatsAppSenderDto>();

            var rows = await _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.WhatsAppBusinessNumber)
                .ToListAsync(ct);

            return rows.Select(x =>
            {
                // inline normalize: uppercase; map META -> META_CLOUD
                var prov = (x.Provider ?? string.Empty).Trim().ToUpperInvariant();
                if (prov == "META") prov = "META_CLOUD";

                return new WhatsAppSenderDto
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    Provider = prov, // "PINNACLE" | "META_CLOUD" (ideally)
                    PhoneNumberId = x.PhoneNumberId,
                    WhatsAppBusinessNumber = x.WhatsAppBusinessNumber,
                    SenderDisplayName = x.SenderDisplayName,
                    IsActive = x.IsActive,
                    IsDefault = x.IsDefault
                };
            }).ToList();
        }
        public async Task<(string Provider, string PhoneNumberId)?> ResolveSenderPairAsync(
            Guid businessId,
            string phoneNumberId,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(phoneNumberId))
                return null;

            var row = await _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.BusinessId == businessId &&
                    x.PhoneNumberId == phoneNumberId &&
                    x.IsActive, ct);

            if (row == null) return null;

            // inline normalize here too
            var prov = (row.Provider ?? string.Empty).Trim().ToUpperInvariant();
            if (prov == "META") prov = "META_CLOUD";

            return (prov, row.PhoneNumberId);
        }
    }
}
