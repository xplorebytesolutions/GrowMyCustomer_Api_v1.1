using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.WhatsAppSettings.DTOs;               
namespace xbytechat.api.WhatsAppSettings.Services
{
    public sealed class WhatsAppSenderService : IWhatsAppSenderService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WhatsAppSenderService> _logger;

        public WhatsAppSenderService(AppDbContext db, ILogger<WhatsAppSenderService> logger)
        {
            _db = db;
            _logger = logger;
        }

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

        public async Task<WhatsAppSenderResolutionResult> ResolveDefaultSenderAsync(
            Guid businessId,
            string? providerHint = null,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                return WhatsAppSenderResolutionResult.Fail(providerHint, "BusinessId is empty.");

            // Normalize provider hint: "META" -> "META_CLOUD", "meta-cloud" -> "META_CLOUD", etc.
            string? provider = null;
            if (!string.IsNullOrWhiteSpace(providerHint))
            {
                provider = providerHint.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
                if (provider == "META") provider = "META_CLOUD";
            }

            // ESU constraint: sender phone_number_id MUST come from WhatsAppPhoneNumbers only (never WhatsAppSettings.PhoneNumberId).
            var q = _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.IsActive);

            if (!string.IsNullOrWhiteSpace(provider))
                q = q.Where(x => x.Provider.ToUpper() == provider);

            // 1) Prefer active default sender.
            var bestDefault = await q
                .Where(x => x.IsDefault)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenBy(x => x.PhoneNumberId)
                .FirstOrDefaultAsync(ct);

            if (bestDefault != null)
            {
                var prov = (bestDefault.Provider ?? string.Empty).Trim().ToUpperInvariant();
                if (prov == "META") prov = "META_CLOUD";

                return WhatsAppSenderResolutionResult.Ok(prov, bestDefault.PhoneNumberId);
            }

            // 2) No default: pick deterministic active sender and emit a warning (guardrail).
            var fallback = await q
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenBy(x => x.PhoneNumberId)
                .FirstOrDefaultAsync(ct);

            if (fallback != null)
            {
                var prov = (fallback.Provider ?? string.Empty).Trim().ToUpperInvariant();
                if (prov == "META") prov = "META_CLOUD";

                var warning = string.IsNullOrWhiteSpace(provider)
                    ? "No default WhatsApp sender is set. Using the most recently updated active sender (any provider)."
                    : $"No default WhatsApp sender is set for provider '{provider}'. Using the most recently updated active sender for that provider.";

                // This log is intentionally inside the resolver so future call sites can't miss the guardrail.
                _logger.LogWarning(
                    "WhatsAppSenderService: {Warning} biz={BusinessId} providerHint={ProviderHint} pickedProvider={PickedProvider} pickedPhoneNumberId={PhoneNumberId}",
                    warning, businessId, providerHint, prov, fallback.PhoneNumberId);

                return WhatsAppSenderResolutionResult.Ok(prov, fallback.PhoneNumberId, warning);
            }

            return WhatsAppSenderResolutionResult.Fail(
                provider,
                string.IsNullOrWhiteSpace(provider)
                    ? "No active WhatsAppPhoneNumbers sender configured for this business."
                    : $"No active WhatsAppPhoneNumbers sender configured for provider '{provider}'.");
        }
    }
}
