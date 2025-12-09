using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.WhatsAppSettings.Models;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Models;

namespace xbytechat.api.Features.WhatsAppSettings.Services
{
    public interface IWhatsAppPhoneNumberService
    {
        Task<IReadOnlyList<WhatsAppPhoneNumber>> ListAsync(Guid businessId, string provider, CancellationToken ct = default);

        Task<WhatsAppPhoneNumber> UpsertAsync(
            Guid businessId,
            string provider,
            string phoneNumberId,
            string whatsAppBusinessNumber,
            string? senderDisplayName,
            bool? isActive = null,
            bool? isDefault = null);

        // Delete by entity Id (GUID)
        Task<bool> DeleteAsync(Guid businessId, string provider, Guid id);

        // Set one number as default (enforces “one default per (business, provider)”)
        Task<bool> SetDefaultAsync(Guid businessId, string provider, Guid id);

        // Find a specific number by PhoneNumberId
        Task<WhatsAppPhoneNumber?> FindAsync(Guid businessId, string provider, string phoneNumberId);

        // Get the default number for a provider (or null if not set)
        Task<WhatsAppPhoneNumber?> GetDefaultAsync(Guid businessId, string provider);

        // in IWhatsAppPhoneNumberService
        Task<(int Added, int Updated, int Total)> SyncFromProviderAsync(
        Guid businessId,
        WhatsAppSettingsDto setting,   // DTO
        string provider,
        CancellationToken ct = default);
    }
}
