using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace xbytechat.api.Features.Webhooks.Services.Processors
{
    public class TemplateWebhookProcessor : ITemplateWebhookProcessor
    {
        private readonly ILogger<TemplateWebhookProcessor> _logger;

        public TemplateWebhookProcessor(ILogger<TemplateWebhookProcessor> logger)
        {
            _logger = logger;
        }

        public async Task ProcessTemplateUpdateAsync(JsonElement payload)
        {
            try
            {
                var entry = payload.GetProperty("entry")[0];
                var changes = entry.GetProperty("changes")[0];
                var value = changes.GetProperty("value");

                var eventType = value.GetProperty("event").GetString();
                var templateId = value.TryGetProperty("message_template_id", out var idProp)
                                 ? idProp.GetString() : "(unknown)";

                _logger.LogInformation($"🧾 Template Event Received: {eventType} for ID: {templateId}");

                // 🧠 You can store in DB or show in admin logs in the future

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process template webhook update.");
            }
        }
    }
}
