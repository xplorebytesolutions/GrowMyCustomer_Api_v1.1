using System.Threading.Tasks;
using xbytechat.api.DTOs.Messages;
using xbytechat.api.Helpers;

namespace xbytechat.api.Services.Messages.Interfaces
{
    public interface IMessageService
    {
        /// <summary>
        /// Sends a message of any supported type (Text, Image, Template).
        /// </summary>
        /// <param name="dto">Base DTO representing message details.</param>
        /// <returns>Standardized result with status, error info, and raw response.</returns>
        Task<SendResultExtended> SendMessageAsync(BaseMessageDto dto);
        //Task<SendResultExtended> SendBulkMessagesAsync(BulkMessageDto dto);
        Task SendFollowUpAsync(string recipientNumber, string messageContent);
        Task<SendResultExtended> SendInteractiveMessageAsync(string recipientPhone, string bodyText, List<string> buttons);


    }
}
