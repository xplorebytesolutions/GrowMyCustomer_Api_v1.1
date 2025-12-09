using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.CampaignModule.SendEngine;

public sealed record ButtonMeta(string Text, string Type, string? TargetUrl);

public sealed record SendPlan(
    Guid BusinessId,
    Provider Provider,
    string PhoneNumberId,
    string TemplateName,
    string LanguageCode,
    HeaderKind HeaderKind,
    string? HeaderUrl,
    IReadOnlyList<ButtonMeta> Buttons
);

public sealed record RecipientPlan(
    Guid RecipientId,
    string ToPhoneE164,
    string ParametersJson,        // e.g. ["p1","p2",...]
    string? ButtonParamsJson,     // optional per-recipient button params json
    string IdempotencyKey
);

public sealed record TemplateEnvelope(
    HeaderKind HeaderKind,
    IReadOnlyList<string> HeaderParams,   // NEW: for TEXT headers
    string? HeaderUrl,                    // for IMAGE/VIDEO/DOCUMENT
    IReadOnlyList<string> BodyParams,
    IReadOnlyList<ButtonMeta> Buttons,
    string? PerRecipientButtonParamsJson
);
