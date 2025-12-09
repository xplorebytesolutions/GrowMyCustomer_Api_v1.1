using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.Webhooks.Services.Processors;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat.api.Features.CustomeApi.Services;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    public class FlowRuntimeService : IFlowRuntimeService
    {
        private readonly AppDbContext _dbContext;
        private readonly IMessageEngineService _messageEngineService;
        private readonly IWhatsAppTemplateFetcherService _templateFetcherService;
        private readonly ILogger<FlowRuntimeService> _logger;
        private readonly ICtaJourneyPublisher _ctaPublisher;
        private readonly IWhatsAppSettingsService _whatsAppSettingsService;
        public FlowRuntimeService(
            AppDbContext dbContext,
            IMessageEngineService messageEngineService,
            IWhatsAppTemplateFetcherService templateFetcherService,  ILogger<FlowRuntimeService> logger, ICtaJourneyPublisher ctaPublisher, IWhatsAppSettingsService whatsAppSettingsService)
        {
            _dbContext = dbContext;
            _messageEngineService = messageEngineService;
            _templateFetcherService = templateFetcherService;
            _logger = logger;
            _ctaPublisher = ctaPublisher;
            _whatsAppSettingsService = whatsAppSettingsService;
        }

        private static string ResolveGreeting(string? profileName, string? contactName)
        {
            var s = (profileName ?? contactName)?.Trim();
            return string.IsNullOrEmpty(s) ? "there" : s;
        }
        private static void EnsureArgsLength(List<string> args, int slot1Based)
        {
            while (args.Count < slot1Based) args.Add(string.Empty);
        }


        //public async Task<NextStepResult> ExecuteNextAsync(NextStepContext context)
        //{
        //    try
        //    {
        //        // ── local helpers ─────────────────────────────────────────────────────────
        //        string ResolveGreeting(string? profileName, string? contactName)
        //        {
        //            var s = (profileName ?? contactName)?.Trim();
        //            return string.IsNullOrEmpty(s) ? "there" : s;
        //        }
        //        void EnsureArgsLength(List<string> args, int slot1Based)
        //        {
        //            while (args.Count < slot1Based) args.Add(string.Empty);
        //        }
        //        // ──────────────────────────────────────────────────────────────────────────

        //        // 1) URL-only buttons → no WA send, just record and return redirect
        //        if (context.ClickedButton != null &&
        //            context.ClickedButton.ButtonType?.Equals("URL", StringComparison.OrdinalIgnoreCase) == true)
        //        {
        //            _dbContext.FlowExecutionLogs.Add(new FlowExecutionLog
        //            {
        //                Id = Guid.NewGuid(),
        //                BusinessId = context.BusinessId,
        //                FlowId = context.FlowId,
        //                StepId = context.SourceStepId,
        //                StepName = "URL_REDIRECT",
        //                MessageLogId = context.MessageLogId,
        //                ButtonIndex = context.ButtonIndex,
        //                ContactPhone = context.ContactPhone,
        //                Success = true,
        //                ExecutedAt = DateTime.UtcNow,
        //                RequestId = context.RequestId
        //            });
        //            await _dbContext.SaveChangesAsync();

        //            return new NextStepResult { Success = true, RedirectUrl = context.ClickedButton.ButtonValue };



        //        }

        //        // 2) Load next step in the same flow (no dedupe/loop guard — always proceed)
        //        var targetStep = await _dbContext.CTAFlowSteps
        //            .Include(s => s.ButtonLinks)
        //            .FirstOrDefaultAsync(s => s.Id == context.TargetStepId &&
        //                                      s.CTAFlowConfigId == context.FlowId);

        //        if (targetStep == null)
        //            return new NextStepResult { Success = false, Error = "Target step not found." };

        //        if (string.IsNullOrWhiteSpace(targetStep.TemplateToSend))
        //            return new NextStepResult { Success = false, Error = "Target step has no template assigned." };

        //        var templateName = targetStep.TemplateToSend.Trim();

        //        // 3) Preflight the template (resolve language and catch 132001 early)
        //        var meta = await _templateFetcherService.GetTemplateByNameAsync(
        //            context.BusinessId, templateName, includeButtons: true);

        //        if (meta == null)
        //        {
        //            _dbContext.FlowExecutionLogs.Add(new FlowExecutionLog
        //            {
        //                Id = Guid.NewGuid(),
        //                BusinessId = context.BusinessId,
        //                FlowId = context.FlowId,
        //                StepId = targetStep.Id,
        //                StepName = templateName,
        //                MessageLogId = null,
        //                ButtonIndex = context.ButtonIndex,
        //                ContactPhone = context.ContactPhone,
        //                Success = false,
        //                ErrorMessage = $"Template '{templateName}' not found for this WABA.",
        //                RawResponse = null,
        //                ExecutedAt = DateTime.UtcNow,
        //                RequestId = context.RequestId
        //            });
        //            await _dbContext.SaveChangesAsync();

        //            return new NextStepResult { Success = false, Error = $"Template '{templateName}' not found or not approved." };
        //        }

        //        var languageCode = string.IsNullOrWhiteSpace(meta.Language) ? "en_US" : meta.Language;

        //        // 3.1) 🔥 Determine sender with failsafes (NO early return for missing context)
        //        var provider = (context.Provider ?? string.Empty).Trim().ToUpperInvariant();
        //        var phoneNumberId = context.PhoneNumberId;

        //        // If provider missing/invalid → try active WhatsAppSettings (fast path)
        //        if (provider != "PINNACLE" && provider != "META_CLOUD")
        //        {
        //            var w = await _dbContext.WhatsAppSettings
        //                .AsNoTracking()
        //                .Where(x => x.BusinessId == context.BusinessId && x.IsActive)
        //                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
        //                .FirstOrDefaultAsync();

        //            if (w != null)
        //            {
        //                provider = (w.Provider ?? "").Trim().ToUpperInvariant();
        //                if (string.IsNullOrWhiteSpace(phoneNumberId))
        //                    phoneNumberId = w.PhoneNumberId;
        //            }
        //        }

        //        // If still missing provider → hard resolve via numbers table
        //        if (provider != "PINNACLE" && provider != "META_CLOUD")
        //        {
        //            var pn = await _dbContext.WhatsAppPhoneNumbers
        //                .AsNoTracking()
        //                .Where(n => n.BusinessId == context.BusinessId && n.IsActive)
        //                .OrderByDescending(n => n.IsDefault)
        //                .ThenBy(n => n.WhatsAppBusinessNumber)
        //                .Select(n => new { n.Provider, n.PhoneNumberId })
        //                .FirstOrDefaultAsync();

        //            if (pn != null)
        //            {
        //                provider = (pn.Provider ?? "").Trim().ToUpperInvariant();
        //                if (string.IsNullOrWhiteSpace(phoneNumberId))
        //                    phoneNumberId = pn.PhoneNumberId;
        //            }
        //        }

        //        if (provider != "PINNACLE" && provider != "META_CLOUD")
        //            return new NextStepResult { Success = false, Error = "No active WhatsApp sender configured (provider could not be resolved)." };

        //        // Ensure we have a sender id
        //        if (string.IsNullOrWhiteSpace(phoneNumberId))
        //        {
        //            phoneNumberId = await _dbContext.WhatsAppPhoneNumbers
        //                .AsNoTracking()
        //                .Where(n => n.BusinessId == context.BusinessId
        //                            && n.IsActive
        //                            && n.Provider.ToUpper() == provider)
        //                .OrderByDescending(n => n.IsDefault)
        //                .ThenBy(n => n.WhatsAppBusinessNumber)
        //                .Select(n => n.PhoneNumberId)
        //                .FirstOrDefaultAsync();

        //            if (string.IsNullOrWhiteSpace(phoneNumberId))
        //                return new NextStepResult { Success = false, Error = "Missing PhoneNumberId (no default sender configured for this provider)." };
        //        }

        //        // ── Profile-name injection into body params (optional) ──────────────────────
        //        var args = new List<string>();
        //        if (targetStep.UseProfileName && targetStep.ProfileNameSlot is int slot && slot >= 1)
        //        {
        //            var contact = await _dbContext.Contacts
        //                .AsNoTracking()
        //                .FirstOrDefaultAsync(c => c.BusinessId == context.BusinessId
        //                                          && c.PhoneNumber == context.ContactPhone);

        //            var greet = ResolveGreeting(contact?.ProfileName, contact?.Name);
        //            EnsureArgsLength(args, slot);
        //            args[slot - 1] = greet;
        //        }

        //        var components = new List<object>();
        //        if (args.Count > 0)
        //        {
        //            components.Add(new
        //            {
        //                type = "body",
        //                parameters = args.Select(a => new { type = "text", text = a ?? string.Empty }).ToList()
        //            });
        //        }
        //        // ───────────────────────────────────────────────────────────────────────────

        //        var payload = new
        //        {
        //            messaging_product = "whatsapp",
        //            to = context.ContactPhone,
        //            type = "template",
        //            template = new
        //            {
        //                name = templateName,
        //                language = new { code = languageCode },
        //                components
        //            }
        //        };

        //        // 4) SEND (explicit provider + sender) — always attempt the POST
        //        _logger.LogInformation("➡️ SEND-INTENT flow={Flow} step={Step} tmpl={T} to={To} provider={Prov}/{Pnid}",
        //            context.FlowId, targetStep.Id, templateName, context.ContactPhone, provider, phoneNumberId);

        //        var sendResult = await _messageEngineService.SendPayloadAsync(
        //            context.BusinessId,
        //            provider,               // explicit
        //            payload,
        //            phoneNumberId           // explicit
        //        );

        //        // 5) Snapshot buttons for robust click mapping later
        //        string? buttonBundleJson = null;
        //        if (targetStep.ButtonLinks?.Count > 0)
        //        {
        //            var bundle = targetStep.ButtonLinks
        //                .OrderBy(b => b.ButtonIndex)
        //                .Select(b => new
        //                {
        //                    i = b.ButtonIndex,
        //                    t = b.ButtonText ?? "",
        //                    ty = b.ButtonType ?? "QUICK_REPLY",
        //                    v = b.ButtonValue ?? "",
        //                    ns = b.NextStepId
        //                })
        //                .ToList();

        //            buttonBundleJson = JsonSerializer.Serialize(bundle);
        //        }

        //        // 6) Write MessageLog
        //        var messageLog = new MessageLog
        //        {
        //            Id = Guid.NewGuid(),
        //            BusinessId = context.BusinessId,
        //            RecipientNumber = context.ContactPhone,
        //            CTAFlowConfigId = context.FlowId,
        //            CTAFlowStepId = targetStep.Id,
        //            FlowVersion = context.Version,
        //            Source = "flow",
        //            RefMessageId = context.MessageLogId,
        //            CreatedAt = DateTime.UtcNow,
        //            Status = sendResult.Success ? "Sent" : "Failed",
        //            MessageId = sendResult.MessageId,
        //            ErrorMessage = sendResult.ErrorMessage,
        //            RawResponse = sendResult.RawResponse,
        //            ButtonBundleJson = buttonBundleJson,
        //            MessageContent = templateName,
        //            SentAt = sendResult.Success ? DateTime.UtcNow : (DateTime?)null
        //        };

        //        _dbContext.MessageLogs.Add(messageLog);

        //        // 7) Flow execution audit row
        //        _dbContext.FlowExecutionLogs.Add(new FlowExecutionLog
        //        {
        //            Id = Guid.NewGuid(),
        //            BusinessId = context.BusinessId,
        //            FlowId = context.FlowId,
        //            StepId = targetStep.Id,
        //            StepName = templateName,
        //            MessageLogId = messageLog.Id,
        //            ButtonIndex = context.ButtonIndex,
        //            ContactPhone = context.ContactPhone,
        //            Success = sendResult.Success,
        //            ErrorMessage = sendResult.ErrorMessage,
        //            RawResponse = sendResult.RawResponse,
        //            ExecutedAt = DateTime.UtcNow,
        //            RequestId = context.RequestId
        //        });

        //        await _dbContext.SaveChangesAsync();

        //        return new NextStepResult
        //        {
        //            Success = sendResult.Success,
        //            Error = sendResult.ErrorMessage,
        //            RedirectUrl = null
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new NextStepResult { Success = false, Error = ex.Message };
        //    }
        //}
        public async Task<NextStepResult> ExecuteNextAsync(NextStepContext context)
        {
            try
            {
                // ── local helpers ─────────────────────────────────────────────────────────
                string ResolveGreeting(string? profileName, string? contactName)
                {
                    var s = (profileName ?? contactName)?.Trim();
                    return string.IsNullOrEmpty(s) ? "there" : s;
                }
                void EnsureArgsLength(List<string> args, int slot1Based)
                {
                    while (args.Count < slot1Based) args.Add(string.Empty);
                }
                // ──────────────────────────────────────────────────────────────────────────

                // 1) URL-only buttons → no WA send, just record and return redirect
                if (context.ClickedButton != null &&
                    context.ClickedButton.ButtonType?.Equals("URL", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _dbContext.FlowExecutionLogs.Add(new FlowExecutionLog
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = context.BusinessId,
                        FlowId = context.FlowId,
                        StepId = context.SourceStepId,
                        StepName = "URL_REDIRECT",
                        MessageLogId = context.MessageLogId,
                        ButtonIndex = context.ButtonIndex,
                        ContactPhone = context.ContactPhone,
                        Success = true,
                        ExecutedAt = DateTime.UtcNow,
                        RequestId = context.RequestId
                    });
                    await _dbContext.SaveChangesAsync();

                    return new NextStepResult { Success = true, RedirectUrl = context.ClickedButton.ButtonValue };
                }

                // 2) Load next step in the same flow
                var targetStep = await _dbContext.CTAFlowSteps
                    .Include(s => s.ButtonLinks)
                    .FirstOrDefaultAsync(s => s.Id == context.TargetStepId &&
                                              s.CTAFlowConfigId == context.FlowId);

                if (targetStep == null)
                    return new NextStepResult { Success = false, Error = "Target step not found." };

                if (string.IsNullOrWhiteSpace(targetStep.TemplateToSend))
                    return new NextStepResult { Success = false, Error = "Target step has no template assigned." };

                var templateName = targetStep.TemplateToSend.Trim();

                // 3) Preflight the template (you can replace with a DB read later if desired)
                var meta = await _templateFetcherService.GetTemplateByNameAsync(
                    context.BusinessId, templateName, includeButtons: true);

                if (meta == null)
                {
                    _dbContext.FlowExecutionLogs.Add(new FlowExecutionLog
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = context.BusinessId,
                        FlowId = context.FlowId,
                        StepId = targetStep.Id,
                        StepName = templateName,
                        MessageLogId = null,
                        ButtonIndex = context.ButtonIndex,
                        ContactPhone = context.ContactPhone,
                        Success = false,
                        ErrorMessage = $"Template '{templateName}' not found for this WABA.",
                        RawResponse = null,
                        ExecutedAt = DateTime.UtcNow,
                        RequestId = context.RequestId
                    });
                    await _dbContext.SaveChangesAsync();

                    return new NextStepResult { Success = false, Error = $"Template '{templateName}' not found or not approved." };
                }

                var languageCode = string.IsNullOrWhiteSpace(meta.Language) ? "en_US" : meta.Language;

                // 3.1) Sender resolution (single source of truth via DTO, with context overrides)
                string provider = (context.Provider ?? string.Empty).Trim().ToUpperInvariant();
                string? phoneNumberId = context.PhoneNumberId;

                if (provider != "PINNACLE" && provider != "META_CLOUD" || string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    // Pull unified settings (provider + default phone for that provider)
                    var wa = await _whatsAppSettingsService.GetSettingsByBusinessIdAsync(context.BusinessId);
                    if (wa == null)
                        return new NextStepResult { Success = false, Error = "No active WhatsApp settings found." };

                    // Context wins if valid, else fall back to DTO
                    if (provider != "PINNACLE" && provider != "META_CLOUD")
                    {
                        var key = (wa.Provider ?? string.Empty).Trim().ToLowerInvariant();
                        provider = key switch
                        {
                            "meta_cloud" => "META_CLOUD",
                            "pinnacle" => "PINNACLE",
                            _ => (wa.Provider ?? string.Empty).Trim().ToUpperInvariant()
                        };
                    }

                    if (string.IsNullOrWhiteSpace(phoneNumberId))
                        phoneNumberId = wa.PhoneNumberId; // can be null for non-meta providers
                }

                if (provider != "PINNACLE" && provider != "META_CLOUD")
                    return new NextStepResult { Success = false, Error = "No active WhatsApp sender configured (provider could not be resolved)." };

                if (provider == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberId))
                    return new NextStepResult { Success = false, Error = "Missing PhoneNumberId (no default Meta sender configured)." };

                // ── Optional profile-name injection into body params ───────────────────────
                var args = new List<string>();
                if (targetStep.UseProfileName && targetStep.ProfileNameSlot is int slot && slot >= 1)
                {
                    var contact = await _dbContext.Contacts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.BusinessId == context.BusinessId
                                                  && c.PhoneNumber == context.ContactPhone);

                    var greet = ResolveGreeting(contact?.ProfileName, contact?.Name);
                    EnsureArgsLength(args, slot);
                    args[slot - 1] = greet;
                }

                var components = new List<object>();
                if (args.Count > 0)
                {
                    components.Add(new
                    {
                        type = "body",
                        parameters = args.Select(a => new { type = "text", text = a ?? string.Empty }).ToList()
                    });
                }
                // ───────────────────────────────────────────────────────────────────────────

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = context.ContactPhone,
                    type = "template",
                    template = new
                    {
                        name = templateName,
                        language = new { code = languageCode },
                        components
                    }
                };

                // 4) SEND (explicit provider + sender)
                _logger.LogInformation("➡️ SEND-INTENT flow={Flow} step={Step} tmpl={T} to={To} provider={Prov}/{Pnid}",
                    context.FlowId, targetStep.Id, templateName, context.ContactPhone, provider, phoneNumberId);

                var sendResult = await _messageEngineService.SendPayloadAsync(
                    context.BusinessId,
                    provider,
                    payload,
                    phoneNumberId
                );

                // 5) Snapshot buttons for click mapping
                string? buttonBundleJson = null;
                if (targetStep.ButtonLinks?.Count > 0)
                {
                    var bundle = targetStep.ButtonLinks
                        .OrderBy(b => b.ButtonIndex)
                        .Select(b => new
                        {
                            i = b.ButtonIndex,
                            t = b.ButtonText ?? "",
                            ty = b.ButtonType ?? "QUICK_REPLY",
                            v = b.ButtonValue ?? "",
                            ns = b.NextStepId
                        })
                        .ToList();

                    buttonBundleJson = JsonSerializer.Serialize(bundle);
                }

                // 6) MessageLog
                var messageLog = new MessageLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = context.BusinessId,
                    RecipientNumber = context.ContactPhone,
                    CTAFlowConfigId = context.FlowId,
                    CTAFlowStepId = targetStep.Id,
                    FlowVersion = context.Version,
                    Source = "flow",
                    RefMessageId = context.MessageLogId,
                    CreatedAt = DateTime.UtcNow,
                    Status = sendResult.Success ? "Sent" : "Failed",
                    MessageId = sendResult.MessageId,
                    ErrorMessage = sendResult.ErrorMessage,
                    RawResponse = sendResult.RawResponse,
                    ButtonBundleJson = buttonBundleJson,
                    MessageContent = templateName,
                    SentAt = sendResult.Success ? DateTime.UtcNow : (DateTime?)null
                };

                _dbContext.MessageLogs.Add(messageLog);

                // 7) Flow execution audit
                _dbContext.FlowExecutionLogs.Add(new FlowExecutionLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = context.BusinessId,
                    FlowId = context.FlowId,
                    StepId = targetStep.Id,
                    StepName = templateName,
                    MessageLogId = messageLog.Id,
                    ButtonIndex = context.ButtonIndex,
                    ContactPhone = context.ContactPhone,
                    Success = sendResult.Success,
                    ErrorMessage = sendResult.ErrorMessage,
                    RawResponse = sendResult.RawResponse,
                    ExecutedAt = DateTime.UtcNow,
                    RequestId = context.RequestId
                });

                await _dbContext.SaveChangesAsync();

                return new NextStepResult
                {
                    Success = sendResult.Success,
                    Error = sendResult.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                return new NextStepResult { Success = false, Error = ex.Message };
            }
        }


    }
}


