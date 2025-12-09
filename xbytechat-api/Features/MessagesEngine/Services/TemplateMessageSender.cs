// 📄 File: Features/MessagesEngine/Services/TemplateMessageSender.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Helpers;

//using xbytechat.api.Helpers;
using xbytechat.api.Shared;
using xbytechat.api.Shared.utility;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Services;

namespace xbytechat.api.Features.MessagesEngine.Services
{
    public class TemplateMessageSender : ITemplateMessageSender
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TemplateMessageSender> _logger;
        private readonly IWhatsAppTemplateFetcherService _templateService;

        public TemplateMessageSender(
            AppDbContext db,
            HttpClient httpClient,
            ILogger<TemplateMessageSender> logger,
            IWhatsAppTemplateFetcherService templateService)
        {
            _db = db;
            _httpClient = httpClient;
            _logger = logger;
            _templateService = templateService;
        }

        //public async Task<ResponseResult> SendTemplateMessageToContactAsync(
        //    Guid businessId,
        //    Contact contact,
        //    string templateName,
        //    List<string> templateParams,
        //    string? imageUrl = null,
        //    List<CampaignButton>? buttons = null,
        //    string? source = null,
        //    Guid? refMessageId = null)
        //{
        //    var setting = await _db.WhatsAppSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId && s.IsActive);
        //    if (setting == null)
        //        return ResponseResult.ErrorInfo("WhatsApp settings not found for this business.");

        //    var template = await _templateService.GetTemplateByNameAsync(businessId, templateName, includeButtons: true);
        //    if (template == null)
        //        return ResponseResult.ErrorInfo("Template not found or invalid.");

        //    var payload = new Dictionary<string, object>
        //    {
        //        ["messaging_product"] = "whatsapp",
        //        ["to"] = contact.PhoneNumber,
        //        ["type"] = "template",
        //        ["template"] = new
        //        {
        //            name = template.Name,
        //            language = new { code = template.Language },
        //            components = BuildTemplateComponents(template, templateParams, imageUrl, buttons)
        //        }
        //    };

        //    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiKey);
        //    var json = JsonSerializer.Serialize(payload);
        //    var response = await _httpClient.PostAsync(
        //        $"{setting.ApiUrl?.TrimEnd('/') ?? "https://graph.facebook.com/v22.0"}/{setting.PhoneNumberId}/messages",
        //        new StringContent(json, Encoding.UTF8, "application/json"));

        //    var responseBody = await response.Content.ReadAsStringAsync();
        //    var status = response.IsSuccessStatusCode ? "Sent" : "Failed";

        //    await _db.MessageLogs.AddAsync(new MessageLog
        //    {
        //        Id = Guid.NewGuid(),
        //        BusinessId = businessId,
        //        ContactId = contact.Id,
        //        MessageContent = template.Name,
        //        MediaUrl = imageUrl,
        //        Status = status,
        //        RawResponse = responseBody,
        //        ErrorMessage = response.IsSuccessStatusCode ? null : responseBody,
        //        Source = source,
        //        RefMessageId = refMessageId,
        //        CreatedAt = DateTime.UtcNow,
        //        SentAt = DateTime.UtcNow
        //    });

        //    await _db.SaveChangesAsync();
        //    return response.IsSuccessStatusCode
        //        ? ResponseResult.SuccessInfo("✅ Message sent successfully", null, responseBody)
        //        : ResponseResult.ErrorInfo("❌ Message failed", null, responseBody);

        //}

        public async Task<ResponseResult> SendTemplateMessageToContactAsync(
    Guid businessId,
    Contact contact,
    string templateName,
    List<string> templateParams,
    string? imageUrl = null,
    List<CampaignButton>? buttons = null,
    string? source = null,
    Guid? refMessageId = null)
        {
            // 1) Load settings (provider, keys, base url, etc.)
            var setting = await _db.WhatsAppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.IsActive);

            if (setting == null)
                return ResponseResult.ErrorInfo("❌ WhatsApp settings not found for this business.");

            // 2) Pick the default phone number for THIS provider from WhatsAppPhoneNumbers
            var phone = await _db.WhatsAppPhoneNumbers
                .AsNoTracking()
                .Where(p => p.BusinessId == businessId
                            && p.IsActive
                            && p.Provider.ToLower() == (setting.Provider ?? string.Empty).ToLower())
                .OrderByDescending(p => p.IsDefault)
                .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .Select(p => new { p.PhoneNumberId, p.WhatsAppBusinessNumber })
                .FirstOrDefaultAsync();

            var provider = (setting.Provider ?? string.Empty).Trim().ToUpperInvariant();
            var phoneNumberId = phone?.PhoneNumberId;  // may be null for Pinnacle if you use WABA id
            var baseUrl = (setting.ApiUrl ?? string.Empty).Trim().TrimEnd('/');

            // 3) Template meta
            var template = await _templateService.GetTemplateByNameAsync(
                businessId, templateName, includeButtons: true);

            if (template == null)
                return ResponseResult.ErrorInfo("❌ Template not found or invalid.");

            // 4) Provider-specific URL + headers
            string url;
            using var req = new HttpRequestMessage(HttpMethod.Post, "");

            if (provider == "META_CLOUD")
            {
                if (string.IsNullOrWhiteSpace(setting.ApiKey))
                    return ResponseResult.ErrorInfo("❌ Meta access token (ApiKey) missing.");
                if (string.IsNullOrWhiteSpace(phoneNumberId))
                    return ResponseResult.ErrorInfo("❌ PhoneNumberId not configured for Meta Cloud.");

                if (string.IsNullOrWhiteSpace(baseUrl))
                    baseUrl = "https://graph.facebook.com/v22.0";

                url = $"{baseUrl}/{phoneNumberId}/messages";
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiKey);
            }
            else if (provider == "PINNACLE")
            {
                if (string.IsNullOrWhiteSpace(setting.ApiKey))
                    return ResponseResult.ErrorInfo("❌ Pinnacle API key missing.");

                if (string.IsNullOrWhiteSpace(baseUrl))
                    baseUrl = "https://partnersv1.pinbot.ai";
                if (!baseUrl.EndsWith("/v3", StringComparison.OrdinalIgnoreCase))
                    baseUrl += "/v3";

                var pathId = !string.IsNullOrWhiteSpace(setting.WabaId)
                                ? setting.WabaId!.Trim()
                                : (phoneNumberId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(pathId))
                    return ResponseResult.ErrorInfo("❌ Provide WABA ID or PhoneNumberId for Pinnacle.");

                url = $"{baseUrl}/{pathId}/messages";
                // put the key everywhere Pinnacle might accept it
                req.Headers.TryAddWithoutValidation("apikey", setting.ApiKey);
                req.Headers.TryAddWithoutValidation("x-api-key", setting.ApiKey);
                req.Headers.Authorization = new AuthenticationHeaderValue("Apikey", setting.ApiKey);
            }
            else
            {
                return ResponseResult.ErrorInfo($"❌ Unsupported provider: {setting.Provider}");
            }

            // 5) Payload (language field differs by provider)
            object components = BuildTemplateComponents(template, templateParams, imageUrl, buttons);
            object payload = (provider == "PINNACLE")
                ? new
                {
                    messaging_product = "whatsapp",
                    to = contact.PhoneNumber,
                    type = "template",
                    template = new
                    {
                        name = template.Name,
                        language = template.Language,    // Pinnacle expects a string
                        components
                    }
                }
                : new
                {
                    messaging_product = "whatsapp",
                    to = contact.PhoneNumber,
                    type = "template",
                    template = new
                    {
                        name = template.Name,
                        language = new { code = template.Language }, // Meta needs { code: "en_US" }
                        components
                    }
                };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            req.RequestUri = new Uri(url);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // 6) Send and log
            var response = await _httpClient.SendAsync(req);
            var responseBody = await response.Content.ReadAsStringAsync();
            var status = response.IsSuccessStatusCode ? "Sent" : "Failed";

            await _db.MessageLogs.AddAsync(new MessageLog
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                ContactId = contact.Id,
                MessageContent = template.Name,
                MediaUrl = imageUrl,
                Status = status,
                RawResponse = responseBody,
                ErrorMessage = response.IsSuccessStatusCode ? null : responseBody,
                Source = source,
                RefMessageId = refMessageId,
                CreatedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            return response.IsSuccessStatusCode
                ? ResponseResult.SuccessInfo("✅ Message sent successfully", null, responseBody)
                : ResponseResult.ErrorInfo("❌ Message failed", null, responseBody);
        }

        public async Task<ResponseResult> SendTemplateCampaignAsync(Campaign campaign)
        {
            if (campaign == null || campaign.IsDeleted)
                return ResponseResult.ErrorInfo("Invalid or deleted campaign.");

            var contacts = await _db.CampaignRecipients
                .Include(r => r.Contact)
                .Where(r => r.CampaignId == campaign.Id && r.Contact != null)
                .ToListAsync();

            if (!contacts.Any())
                return ResponseResult.ErrorInfo("No contacts found for this campaign.");

            var templateName = campaign.TemplateId;
            var templateParams = TemplateParameterHelper.ParseTemplateParams(campaign.TemplateParameters);
            var templateMeta = await _templateService.GetTemplateByNameAsync(campaign.BusinessId, templateName, includeButtons: true);

            if (templateMeta == null)
                return ResponseResult.ErrorInfo("Template metadata not found.");

            int success = 0, failed = 0;

            foreach (var r in contacts)
            {
                var result = await SendTemplateMessageToContactAsync(
                    campaign.BusinessId,
                    r.Contact,
                    templateName,
                    templateParams,
                    campaign.ImageUrl,
                    campaign.MultiButtons?.ToList(),
                    source: "campaign",
                    refMessageId: campaign.Id);

                await _db.CampaignSendLogs.AddAsync(new CampaignSendLog
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    ContactId = r.ContactId,
                    RecipientId = r.Id,
                    MessageBody = campaign.MessageBody ?? templateName,
                    TemplateId = templateName,
                    SendStatus = result.Success ? "Sent" : "Failed",
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    CreatedBy = campaign.CreatedBy
                });

                if (result.Success) success++;
                else failed++;
            }

            await _db.SaveChangesAsync();
            return ResponseResult.SuccessInfo($"📤 Sent to {success}, ❌ Failed for {failed}.");
        }

        private List<object> BuildTemplateComponents(
            TemplateMetadataDto template,
            List<string> paramsList,
            string? imageUrl,
            List<CampaignButton>? buttons)
        {
            var components = new List<object>();

            if (template.HasImageHeader && !string.IsNullOrWhiteSpace(imageUrl))
            {
                components.Add(new
                {
                    type = "header",
                    parameters = new[] { new { type = "image", image = new { link = imageUrl } } }
                });
            }

            if (paramsList.Any())
            {
                components.Add(new
                {
                    type = "body",
                    parameters = paramsList.Select(p => new { type = "text", text = p }).ToList()
                });
            }

            if (buttons != null && buttons.Any())
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    var btn = buttons[i];
                    components.Add(new
                    {
                        type = "button",
                        sub_type = btn.Type?.ToLower() == "url" ? "url" : "quick_reply",
                        index = i.ToString(),
                        parameters = new[] {
                            new {
                                type = "text",
                                text = btn.Value ?? btn.Title
                            }
                        }
                    });
                }
            }

            return components;
        }
    }
}
