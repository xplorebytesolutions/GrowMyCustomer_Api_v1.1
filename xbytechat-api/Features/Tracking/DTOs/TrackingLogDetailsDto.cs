using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.MessageManagement.DTOs;

namespace xbytechat.api.Features.Tracking.DTOs
{
    public class TrackingLogDetailsDto
    {
        public TrackingLogDto Tracking { get; set; } = new();
        public ContactDto? Contact { get; set; }
        public CampaignDto? Campaign { get; set; }
        public MessageLogDto? MessageLog { get; set; }
    }
}
