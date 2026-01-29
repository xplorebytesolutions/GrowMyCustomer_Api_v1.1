using System;

namespace xbytechat.api.WhatsAppSettings.DTOs
{
    /// <summary>
    /// ESU-aware sender resolution result.
    /// IMPORTANT: PhoneNumberId must be sourced ONLY from WhatsAppPhoneNumbers (never from WhatsAppSettings.PhoneNumberId).
    /// </summary>
    public sealed class WhatsAppSenderResolutionResult
    {
        public bool Success { get; init; }
        public string? Provider { get; init; }
        public string? PhoneNumberId { get; init; }

        /// <summary>Non-fatal warning that should be logged by the caller.</summary>
        public string? Warning { get; init; }

        /// <summary>Fatal reason (e.g., no active sender configured).</summary>
        public string? Error { get; init; }

        public static WhatsAppSenderResolutionResult Ok(string provider, string phoneNumberId, string? warning = null)
            => new()
            {
                Success = true,
                Provider = provider,
                PhoneNumberId = phoneNumberId,
                Warning = warning
            };

        public static WhatsAppSenderResolutionResult Fail(string? provider, string error)
            => new()
            {
                Success = false,
                Provider = provider,
                Error = error
            };
    }
}

