using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Shared;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Helpers;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.MessagesEngine.Services
{
    public interface ITemplateMessageSender
    {
        Task<ResponseResult> SendTemplateMessageToContactAsync(
           Guid businessId,
           Contact contact,
           string templateName,
           List<string> templateParams,
           string? imageUrl = null,
           List<CampaignButton>? buttons = null,
           string? source = null,
           Guid? refMessageId = null
       );

        Task<ResponseResult> SendTemplateCampaignAsync(Campaign campaign);
    }
}

