// Features/CampaignTracking/Worker/ClickEvent.cs
namespace xbytechat.api.Features.CampaignTracking.Worker
{
    public sealed record ClickEvent(
        Guid CampaignSendLogId,
        int ButtonIndex,
        string ButtonTitle,
        string Destination,
        DateTime ClickedAtUtc,
        string Ip,
        string UserAgent,
         string ClickType
         
    );
}
