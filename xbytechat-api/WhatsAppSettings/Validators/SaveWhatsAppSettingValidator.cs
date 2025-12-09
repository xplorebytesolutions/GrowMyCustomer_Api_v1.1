// 📄 WhatsAppSettings/Validators/SaveWhatsAppSettingValidator.cs
#nullable enable
using FluentValidation;
using xbytechat_api.WhatsAppSettings.DTOs;

namespace xbytechat_api.WhatsAppSettings.Validators
{
    public sealed class SaveWhatsAppSettingValidator : AbstractValidator<SaveWhatsAppSettingDto>
    {
        public SaveWhatsAppSettingValidator()
        {
            // Provider
            RuleFor(x => x.Provider)
                .NotEmpty()
                .Must(p => IsMeta(p) || IsPinnacle(p))
                .WithMessage("Provider must be PINNACLE or META_CLOUD.");

            // ApiUrl: ONLY required when active.
            When(x => x.IsActive, () =>
            {
                RuleFor(x => x.ApiUrl)
                    .NotEmpty()
                    .WithMessage("ApiUrl is required when settings are active.");
            });

            // META_CLOUD rules
            When(x => IsMeta(x.Provider), () =>
            {
                RuleFor(x => x.ApiKey)
                    .NotEmpty()
                    .WithMessage("Meta access token (ApiKey) is required for Meta Cloud.");

                // No PhoneNumberId requirement here.
                // WabaId is allowed but not mandatory (can be filled via ESU discovery/sync).
            });

            // PINNACLE rules
            When(x => IsPinnacle(x.Provider), () =>
            {
                RuleFor(x => x.ApiKey)
                    .NotEmpty()
                    .WithMessage("API Key is required for Pinnacle.");

                RuleFor(x => x)
                    .Must(d =>
                        !string.IsNullOrWhiteSpace(d.WabaId)
                        || !string.IsNullOrWhiteSpace(d.PhoneNumberId))
                    .WithMessage("WABA ID or PhoneNumberId required for Pinnacle.");
            });
        }

        // ── helpers ──────────────────────────────────────────────

        private static string Canon(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Trim().ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }

        private static bool IsMeta(string? raw)
            => Canon(raw) == "META_CLOUD"
               || Canon(raw) == "META"
               || Canon(raw) == "WA_CLOUD"
               || Canon(raw) == "WHATSAPP_CLOUD"
               || Canon(raw) == "FACEBOOK";

        private static bool IsPinnacle(string? raw)
            => Canon(raw) == "PINNACLE"
               || Canon(raw) == "PINBOT"
               || Canon(raw) == "PINNACLE_OFFICIAL";
    }
}
