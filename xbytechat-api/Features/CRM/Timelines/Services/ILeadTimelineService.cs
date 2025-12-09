using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.Catalog.Models;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CRM.Timelines.Services
{
    public interface ILeadTimelineService
    {
        Task<LeadTimeline> AddTimelineEntryAsync(LeadTimelineDto dto);
        Task<List<LeadTimeline>> GetTimelineByContactIdAsync(Guid contactId);
        Task<List<LeadTimelineDto>> GetAllTimelinesAsync();
        Task AddFromCatalogClickAsync(CatalogClickLog log);
        Task<ResponseResult> LogCampaignSendAsync(CampaignTimelineLogDto dto);


    }
}
