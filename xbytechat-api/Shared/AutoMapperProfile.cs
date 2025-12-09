using AutoMapper;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.DTOs.Messages;
using xbytechat.api.Features.Tracking.DTOs;
using xbytechat.api.Features.Tracking.Models;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Shared
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Contact, ContactDto>();
            CreateMap<Campaign, CampaignDto>();
            CreateMap<MessageLog, MessageLogDto>();
            CreateMap<TrackingLog, TrackingLogDto>();
        }
    }
}
