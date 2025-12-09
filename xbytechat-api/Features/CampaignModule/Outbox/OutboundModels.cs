using xbytechat.api.Features.CampaignModule.SendEngine;

namespace XByteChat.api.Features.CampaignModule.Outbox;

public sealed record OutboundItem(
    Guid CampaignId, Guid BusinessId, Provider Provider, string PhoneNumberId,
    string TemplateName, string LanguageCode, HeaderKind HeaderKind, string? HeaderUrl,
    string ParametersJson, string? ButtonParamsJson,
    string ToPhoneE164, Guid RecipientId, string IdempotencyKey
);
