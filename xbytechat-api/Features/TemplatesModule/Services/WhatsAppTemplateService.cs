using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat.api.Features.TemplateModule.Services
{
    public class WhatsAppTemplateService : IWhatsAppTemplateService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<WhatsAppTemplateService> _logger;

        public WhatsAppTemplateService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppTemplateService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<List<TemplateMetadataDto>> FetchTemplatesAsync()
        {
            var wabaId = _config["WhatsApp:WABA_ID"];
            var token = _config["WhatsApp:apiToken"];
            var url = $"https://graph.facebook.com/v18.0/{wabaId}/message_templates";

            var templates = new List<TemplateMetadataDto>();

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch WhatsApp templates: " + json);
                    return templates;
                }

                var parsed = JsonConvert.DeserializeObject<dynamic>(json);

                foreach (var tpl in parsed.data)
                {
                    string name = tpl.name;
                    string language = tpl.language ?? "en_US";
                    string body = "";

                    foreach (var component in tpl.components)
                    {
                        if (component.type == "BODY")
                        {
                            body = component.text;
                            break;
                        }
                    }

                    // Count {{placeholders}}
                    var placeholderCount = System.Text.RegularExpressions.Regex.Matches(body, "{{(.*?)}}").Count;

                    templates.Add(new TemplateMetadataDto
                    {
                        Name = name,
                        Language = language,
                        Body = body,
                        PlaceholderCount = placeholderCount
                    });
                }

                return templates;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while fetching templates from Meta: " + ex.Message);
                return templates;
            }
        }
    }
}
