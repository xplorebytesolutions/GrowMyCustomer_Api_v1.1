namespace xbytechat.api.Features.CTAFlowBuilder.DTOs
{
    public sealed record AttachedCampaignDto(
        Guid Id,
        string Name,
        string Status,
        DateTime? ScheduledAt,
        DateTime CreatedAt,
        string? CreatedBy,
        DateTime? FirstSentAt   // earliest non-null SentAt from CampaignSendLogs
    );


}
