using System;

namespace xbytechat.api.Features.CampaignTracking.Logging
{
    public record CampaignLogRecord(
        Guid Id,
        Guid? RunId,
        string? MessageId,
        Guid CampaignId,
        Guid? ContactId,
        Guid RecipientId,
        string MessageBody,
        string? TemplateId,
        string? SendStatus,
        string? ErrorMessage,
        DateTime CreatedAt,
        string? CreatedBy,
        DateTime? SentAt,
        DateTime? DeliveredAt,
        DateTime? ReadAt,
        string? IpAddress,
        string? DeviceInfo,
        string? MacAddress,
        string? SourceChannel,
        string? DeviceType,
        string? Browser,
        string? Country,
        string? City,
        bool IsClicked,
        DateTime? ClickedAt,
        string? ClickType,
        int RetryCount,
        DateTime? LastRetryAt,
        string? LastRetryStatus,
        bool AllowRetry,
        Guid? MessageLogId,
        Guid BusinessId,
        Guid? CTAFlowConfigId,
        Guid? CTAFlowStepId,
        string? ButtonBundleJson
    );
}
