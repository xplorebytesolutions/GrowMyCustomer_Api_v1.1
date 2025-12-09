namespace xbytechat.api.Features.Tracking.Services
{
    public interface IUrlBuilderService
    {

         string BuildTrackedButtonUrl(
        Guid campaignSendLogId,
        int buttonIndex,
        string? buttonTitle,
        string destinationUrlAbsolute);
    }
}
