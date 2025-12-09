using System.Text;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using xbytechat.api.DTOs.Messages;
using xbytechat.api.Helpers;
using xbytechat.api.Models;
using xbytechat.api.PayloadBuilders;
using xbytechat.api.Repositories.Interfaces;
using xbytechat.api.Services.Messages.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace xbytechat.api.Services.Messages.Implementations
{
    public class MessageService : IMessageService
    {
        private readonly AppDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IGenericRepository<MessageLog> _messageLogRepo;
        private readonly Dictionary<string, IWhatsAppPayloadBuilder> _payloadBuilders;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            AppDbContext dbContext,
            HttpClient httpClient,
            IConfiguration config,
            IGenericRepository<MessageLog> messageLogRepo,
            IEnumerable<IWhatsAppPayloadBuilder> builders,
            ILogger<MessageService> logger)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _config = config;
            _messageLogRepo = messageLogRepo;
            _logger = logger;

            _payloadBuilders = builders.ToDictionary(
                b => b.GetType().Name.Replace("MessagePayloadBuilder", "").ToLower(),
                b => b
            );
        }

        public async Task SendFollowUpAsync(string recipientNumber, string messageContent)
        {
            var dto = new TextMessageDto
            {
                RecipientNumber = recipientNumber,
                MessageContent = messageContent,
                BusinessId = Guid.Empty // Optional: Set dynamically if needed
            };

            await SendMessageAsync(dto); // ✅ You already have this method
        }


        public async Task<SendResultExtended> SendMessageAsync(BaseMessageDto dto)
        {
            var messageType = dto.GetType().Name.Replace("MessageDto", "").ToLower();

            // 🧠 Get the right builder (e.g., for text, image)
            if (!_payloadBuilders.TryGetValue(messageType, out var builder))
            {
                return new SendResultExtended
                {
                    Success = false,
                    Message = "❌ Unsupported message type: " + messageType
                };
            }

            var apiUrl = _config["WhatsApp:ApiUrl"];
            var apiToken = _config["WhatsApp:apiToken"];
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var payload = builder.BuildPayload(dto);
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("📦 Sending Payload: {Payload}", json);

            try


            {
                // 🛠️ Log the payload for debugging
                _logger.LogInformation("📤 Final WhatsApp Payload:\n" + JsonConvert.SerializeObject(payload, Formatting.Indented));

                var response = await _httpClient.PostAsync(apiUrl, content);
                var rawResponse = await response.Content.ReadAsStringAsync();

                string? messageId = null;

                // 🧾 Try extracting messageId (WAMID) from response
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var jsonObj = JsonConvert.DeserializeObject<dynamic>(rawResponse);
                        messageId = jsonObj?.messages?[0]?.id;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("✅ Message sent but failed to parse WAMID: " + ex.Message);
                    }
                }

                // 📝 Log message for tracking
                var log = new MessageLog
                {
                    BusinessId = dto.BusinessId,
                    RecipientNumber = dto.RecipientNumber,
                    MessageContent = dto is TextMessageDto textDto && !string.IsNullOrEmpty(textDto.MessageContent)
                        ? textDto.MessageContent
                        : "[Empty or Non-Text]",
                    MediaUrl = (dto as ImageMessageDto)?.MediaUrl,
                    Status = response.IsSuccessStatusCode ? "Sent" : "Failed",
                    ErrorMessage = response.IsSuccessStatusCode ? null : response.ReasonPhrase,
                    RawResponse = rawResponse,
                    SentAt = DateTime.UtcNow,
                    MessageId = messageId
                };

                await _messageLogRepo.AddAsync(log);
                await _messageLogRepo.SaveAsync();

                return new SendResultExtended
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ? "✅ Message sent successfully" : "❌ Failed to send message",
                    MessageId = messageId,
                    RawResponse = rawResponse,
                    MessageLogId = log.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception during message send");

                return new SendResultExtended
                {
                    Success = false,
                    Message = "❌ Exception while sending",
                    ErrorMessage = ex.Message
                };
            }
        }
        public async Task<SendResultExtended> SendInteractiveMessageAsync(string recipientPhone, string bodyText, List<string> buttons)
        {
            var apiUrl = _config["WhatsApp:ApiUrl"];
            var apiToken = _config["WhatsApp:apiToken"];

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var payload = new
            {
                messaging_product = "whatsapp",
                to = recipientPhone,
                type = "interactive",
                interactive = new
                {
                    type = "button",
                    body = new { text = bodyText },
                    action = new
                    {
                        buttons = buttons.Select((text, index) => new
                        {
                            type = "reply",
                            reply = new
                            {
                                id = $"cta_{index + 1}",
                                title = text
                            }
                        }).ToList()
                    }
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("🚀 Sending CTA Message: " + json);

            try
            {
                var response = await _httpClient.PostAsync(apiUrl, content);
                var rawResponse = await response.Content.ReadAsStringAsync();
                // Message send here successfully 
                string? messageId = null;
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        dynamic jsonObj = JsonConvert.DeserializeObject<dynamic>(rawResponse);
                        messageId = jsonObj?.messages?[0]?.id;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Sent, but failed to parse messageId: " + ex.Message);
                    }
                }

                // Log to MessageLogs table (optional: use a dummy entry)

                var log = new MessageLog
                {

                    //BusinessId = Guid.Parse("put-a-valid-business-guid-here"), //Guid.Empty, // set properly if you want to track
                    BusinessId = Guid.TryParse("45262049-0127-4658-93e1-b3ffea645f4f", out var parsedId)
    ? parsedId
    : throw new FormatException("❌ Invalid GUID format used for BusinessId."),
                    RecipientNumber = recipientPhone,
                    MessageContent = bodyText,
                    Status = response.IsSuccessStatusCode ? "Sent" : "Failed",
                    ErrorMessage = response.IsSuccessStatusCode ? null : response.ReasonPhrase,
                    RawResponse = rawResponse,
                    SentAt = DateTime.UtcNow,
                    MessageId = messageId
                };

                await _messageLogRepo.AddAsync(log);
                await _messageLogRepo.SaveAsync();

                return new SendResultExtended
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ? "✅ CTA message sent" : "❌ Failed to send CTA",
                    MessageId = messageId,
                    RawResponse = rawResponse,
                    MessageLogId = log.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception while sending CTA");

                return new SendResultExtended
                {
                    Success = false,
                    Message = "❌ Exception while sending CTA",
                    ErrorMessage = ex.InnerException?.Message ?? ex.Message // ✅ this is critical
                };
            }

        }

    }
}





