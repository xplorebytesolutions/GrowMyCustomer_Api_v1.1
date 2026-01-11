using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.Inbox.Repositories;
using xbytechat.api.Features.MessageManagement.DTOs;

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
            // ✅ Soft idempotency on (BusinessId + ProviderMessageId) if available.
            // Normalize ProviderMessageId to avoid "space" duplicates.
            var providerMessageId = string.IsNullOrWhiteSpace(dto.ProviderMessageId)
                ? null
                : dto.ProviderMessageId.Trim();

            if (!string.IsNullOrWhiteSpace(providerMessageId))
            {
                var existing = await _repository.FindByProviderMessageIdAsync(dto.BusinessId, providerMessageId);
                if (existing != null)
                    return existing;
            }

            // ✅ SentAt: caller should pass provider timestamp when available; otherwise fall back to server time.
            var sentAtUtc = dto.SentAt == default ? DateTime.UtcNow : dto.SentAt;

            var message = new MessageLog
            {
                Id = Guid.NewGuid(),
                BusinessId = dto.BusinessId,

                RecipientNumber = dto.RecipientPhone,
                MessageContent = dto.MessageBody,

                MediaId = dto.MediaId,
                MediaType = dto.MediaType,
                FileName = dto.FileName,
                MimeType = dto.MimeType,
                LocationLatitude = dto.LocationLatitude,
                LocationLongitude = dto.LocationLongitude,
                LocationName = dto.LocationName,
                LocationAddress = dto.LocationAddress,

                IsIncoming = true,

                // ✅ Keep status consistent for UI (incoming should never be updated by delivery webhooks now)
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Received" : dto.Status.Trim(),
                SentAt = sentAtUtc,

                // ✅ CreatedAt = insert time (server truth)
                CreatedAt = DateTime.UtcNow,

                ProviderMessageId = providerMessageId,

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
            // Outgoing WAMID may be unknown at creation time.
            // If present (e.g., send returns WAMID and we call this after), add idempotency to avoid duplicates.
            var providerMessageId = string.IsNullOrWhiteSpace(dto.ProviderMessageId)
                ? null
                : dto.ProviderMessageId.Trim();

            if (!string.IsNullOrWhiteSpace(providerMessageId))
            {
                var existing = await _repository.FindByProviderMessageIdAsync(dto.BusinessId, providerMessageId);
                if (existing != null)
                    return existing;
            }

            var sentAtUtc = dto.SentAt == default ? DateTime.UtcNow : dto.SentAt;

            var message = new MessageLog
            {
                Id = Guid.NewGuid(),
                BusinessId = dto.BusinessId,

                RecipientNumber = dto.RecipientPhone,
                MessageContent = dto.MessageBody,

                MediaId = dto.MediaId,
                MediaType = dto.MediaType,
                FileName = dto.FileName,
                MimeType = dto.MimeType,
                LocationLatitude = dto.LocationLatitude,
                LocationLongitude = dto.LocationLongitude,
                LocationName = dto.LocationName,
                LocationAddress = dto.LocationAddress,

                IsIncoming = false,

                // ✅ Default outgoing status
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Queued" : dto.Status.Trim(),
                SentAt = sentAtUtc,

                CreatedAt = DateTime.UtcNow,

                ProviderMessageId = providerMessageId,

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
                RecipientNumber = m.RecipientNumber,
                MessageContent = m.MessageContent,
                CreatedAt = m.CreatedAt,
                IsIncoming = m.IsIncoming,
                RenderedBody = m.RenderedBody,
                CampaignId = m.CampaignId,
                CampaignName = m.SourceCampaign?.Name,
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
