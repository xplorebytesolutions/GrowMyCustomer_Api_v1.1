using xbytechat.api.Features.ChatInbox.DTOs;

namespace xbytechat.api.Features.ChatInbox.Services
{
    public interface IChatInboxCommandService
    {
        /// <summary>
        /// Sends an agent-authored text message to a contact from the Chat Inbox
        /// and returns the resulting message DTO for the UI bubble.
        /// </summary>
        Task<ChatInboxMessageDto> SendAgentMessageAsync(
            ChatInboxSendMessageRequestDto request,
            CancellationToken ct = default);
        Task MarkConversationAsReadAsync(
            ChatInboxMarkReadRequestDto request,
            CancellationToken ct = default);

        Task AssignConversationAsync(
           ChatInboxAssignRequestDto request,
           CancellationToken ct = default);

        /// <summary>
        /// Unassigns a conversation (sets AssignedAgentId to null).
        /// </summary>
        Task UnassignConversationAsync(
            ChatInboxUnassignRequestDto request,
            CancellationToken ct = default);

        Task ChangeConversationStatusAsync(
           ChatInboxChangeStatusRequestDto request,
           CancellationToken ct = default);
    }
}
