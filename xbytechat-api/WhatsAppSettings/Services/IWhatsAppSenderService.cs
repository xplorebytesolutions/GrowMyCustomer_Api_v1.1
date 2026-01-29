using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat.api.WhatsAppSettings.Services
{
    public interface IWhatsAppSenderService
    {
        Task<IReadOnlyList<WhatsAppSenderDto>> GetBusinessSendersAsync(Guid businessId, CancellationToken ct = default);
        Task<(string Provider, string PhoneNumberId)?> ResolveSenderPairAsync(Guid businessId, string phoneNumberId, CancellationToken ct = default);

        /// <summary>
        /// Resolve a sender for outbound sends.
        /// ESU constraint: PhoneNumberId MUST be resolved exclusively from WhatsAppPhoneNumbers.
        /// </summary>
        Task<WhatsAppSenderResolutionResult> ResolveDefaultSenderAsync(
            Guid businessId,
            string? providerHint = null,
            CancellationToken ct = default);
    }
}
