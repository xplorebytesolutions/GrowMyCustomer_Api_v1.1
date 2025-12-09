using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.Inbox.Repositories;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Inbox.Services
{
    public class InboxService : IInboxService
    {
        private readonly IInboxRepository _repository;

        public InboxService(IInboxRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<MessageLog>> GetConversationAsync(Guid businessId, string userPhone, string contactPhone, int limit = 50)
        {
            return await _repository.GetConversationAsync(businessId, userPhone, contactPhone, limit);
        }

        public async Task<MessageLog> SaveIncomingMessageAsync(InboxMessageDto dto)
        {
            var message = new MessageLog
            {
                Id = Guid.NewGuid(),
                BusinessId = dto.BusinessId,
                RecipientNumber = dto.RecipientPhone,
                MessageContent = dto.MessageBody,
                IsIncoming = true,
                CreatedAt = DateTime.UtcNow,
                ContactId = dto.ContactId,
                CTAFlowStepId = dto.CTAFlowStepId,
                CTAFlowConfigId = dto.CTAFlowConfigId,
                CampaignId = dto.CampaignId,
                RenderedBody = dto.RenderedBody
            };

            await _repository.AddMessageAsync(message);
            await _repository.SaveChangesAsync();

            return message;
        }

        public async Task<MessageLog> SaveOutgoingMessageAsync(InboxMessageDto dto)
        {
            var message = new MessageLog
            {
                Id = Guid.NewGuid(),
                BusinessId = dto.BusinessId,
                RecipientNumber = dto.RecipientPhone,
                MessageContent = dto.MessageBody,
                IsIncoming = false,
                CreatedAt = DateTime.UtcNow,
                ContactId = dto.ContactId,
                CTAFlowStepId = dto.CTAFlowStepId,
                CTAFlowConfigId = dto.CTAFlowConfigId,
                CampaignId = dto.CampaignId,
                RenderedBody = dto.RenderedBody
            };

            await _repository.AddMessageAsync(message);
            await _repository.SaveChangesAsync();

            return message;
        }
   
        public async Task<List<MessageLogDto>> GetMessagesByContactAsync(Guid businessId, Guid contactId)
        {
            var messages = await _repository.GetMessagesByContactIdAsync(businessId, contactId);

            return messages.Select(m => new MessageLogDto
            {
                Id = m.Id,
                ContactId = m.ContactId,
                RecipientNumber = m.RecipientNumber, // ✅ optional but helpful
                MessageContent = m.MessageContent,
                CreatedAt = m.CreatedAt,
                IsIncoming = m.IsIncoming,
                RenderedBody = m.RenderedBody,
                CampaignId = m.CampaignId,
                CampaignName = m.SourceCampaign?.Name, // ✅ ✅ This is crucial
                CTAFlowConfigId = m.CTAFlowConfigId,
                CTAFlowStepId = m.CTAFlowStepId
            }).ToList();
        }


        public async Task<Dictionary<Guid, int>> GetUnreadMessageCountsAsync(Guid businessId)
        {
            return await _repository.GetUnreadMessageCountsAsync(businessId);
        }
        public async Task MarkMessagesAsReadAsync(Guid businessId, Guid contactId)
        {
            await _repository.MarkMessagesAsReadAsync(businessId, contactId);
        }
        public async Task<Dictionary<Guid, int>> GetUnreadCountsForUserAsync(Guid businessId, Guid userId)
        {
            return await _repository.GetUnreadCountsForUserAsync(businessId, userId);
        }


    }
}
