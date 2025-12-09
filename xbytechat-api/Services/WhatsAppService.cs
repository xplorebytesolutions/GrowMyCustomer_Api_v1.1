using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using xbytechat.api.Models;

namespace xbytechat.api.Services
{
    public class WhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly string _whatsAppToken;
        private readonly string _whatsAppPhoneId;

        public WhatsAppService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();

            _whatsAppToken = configuration["WhatsApp:apiToken"];
            _whatsAppPhoneId = configuration["WhatsApp:PhoneNumberId"];

            if (string.IsNullOrEmpty(_whatsAppToken))
                Console.WriteLine("❌ Token is NULL or EMPTY from config!");

            if (string.IsNullOrEmpty(_whatsAppPhoneId))
                Console.WriteLine("❌ Phone ID is NULL or EMPTY from config!");
        }

        public async Task<WhatsAppResult> SendMessageAsync(string recipientPhone, string messageText)
        {
            try
            {
                Console.WriteLine("👉 Preparing to send WhatsApp message...");
                var url = $"https://graph.facebook.com/v22.0/{_whatsAppPhoneId}/messages";

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = recipientPhone,
                    type = "text",
                    text = new { body = messageText }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _whatsAppToken);

                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"✅ Status: {response.StatusCode}");
                Console.WriteLine($"📥 Response: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    return new WhatsAppResult { Success = true, RawResponse = responseBody };
                }
                else
                {
                    return new WhatsAppResult
                    {
                        Success = false,
                        ErrorMessage = $"Meta API Error: {response.StatusCode}",
                        RawResponse = responseBody
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Exception while sending:");
                Console.WriteLine(ex.Message);

                return new WhatsAppResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

    }
}
