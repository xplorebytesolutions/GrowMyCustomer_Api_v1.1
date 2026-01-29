using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.Webhooks.Services.Processors;
using xbytechat.api.WhatsAppSettings.Services;
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
        private readonly IWhatsAppSenderService _whatsAppSenderService;
        public FlowRuntimeService(
            AppDbContext dbContext,
            IMessageEngineService messageEngineService,
            IWhatsAppTemplateFetcherService templateFetcherService,
            ILogger<FlowRuntimeService> logger,
            ICtaJourneyPublisher ctaPublisher,
            IWhatsAppSettingsService whatsAppSettingsService,
            IWhatsAppSenderService whatsAppSenderService)
        {
            _dbContext = dbContext;
            _messageEngineService = messageEngineService;
            _templateFetcherService = templateFetcherService;
            _logger = logger;
            _ctaPublisher = ctaPublisher;
            _whatsAppSettingsService = whatsAppSettingsService;
            _whatsAppSenderService = whatsAppSenderService;
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

        // NOTE: Keep provider normalization consistent across settings/campaign/webhook/runtime paths.
        // Also enforces "META" -> "META_CLOUD" canonical mapping.
        private static string NormalizeProvider(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var p = raw.Trim().Replace("-", "_").Replace(" ", "_").ToUpperInvariant();
            return p == "META" ? "META_CLOUD" : p;
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
        //                    phoneNumberId = null; // legacy WhatsAppSettings.PhoneNumberId is intentionally not used (ESU split)
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
            // Capture state for exception logging + failure persistence (updated as we resolve sender/template).
            var providerForLog = NormalizeProvider(context.Provider);
            string? phoneNumberIdForLog = string.IsNullOrWhiteSpace(context.PhoneNumberId) ? null : context.PhoneNumberId!.Trim();
            string? templateNameForLog = null;
            Guid? targetStepIdForLog = context.TargetStepId;

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

                // Helpful for webhook-click observability (may be null for non-click paths).
                var clickedBtnText = context.ClickedButton?.ButtonText;

                // 2) Load next step in the same flow
                var targetStep = await _dbContext.CTAFlowSteps
                    .Include(s => s.ButtonLinks)
                    .FirstOrDefaultAsync(s => s.Id == context.TargetStepId &&
                                              s.CTAFlowConfigId == context.FlowId);

                if (targetStep == null)
                {
                    // NOTE: Added for ESU-era click-triggered flows so failures are observable (not silent).
                    var err = "Target step not found.";
                    _logger.LogWarning(
                        "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} to={To} btnIdx={BtnIdx} btnText='{BtnText}' providerHint={Provider}/{PhoneNumberId}",
                        err, context.BusinessId, context.FlowId, context.SourceStepId, context.TargetStepId, context.ContactPhone, context.ButtonIndex, clickedBtnText, context.Provider, context.PhoneNumberId);

                    await RecordFailureAsync(
                        context,
                        // TargetStepId is nullable; store failure against SourceStepId and include target in the error message/logs.
                        stepId: context.SourceStepId,
                        stepName: "TARGET_STEP_NOT_FOUND",
                        error: err);

                    return new NextStepResult { Success = false, Error = err };
                }

                if (string.IsNullOrWhiteSpace(targetStep.TemplateToSend))
                {
                    var err = "Target step has no template assigned.";
                    _logger.LogWarning(
                        "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} to={To} btnIdx={BtnIdx} btnText='{BtnText}' providerHint={Provider}/{PhoneNumberId}",
                        err, context.BusinessId, context.FlowId, context.SourceStepId, context.TargetStepId, context.ContactPhone, context.ButtonIndex, clickedBtnText, context.Provider, context.PhoneNumberId);

                    await RecordFailureAsync(
                        context,
                        stepId: targetStep.Id,
                        stepName: "NO_TEMPLATE_ASSIGNED",
                        error: err);

                    return new NextStepResult { Success = false, Error = err };
                }

                var templateName = targetStep.TemplateToSend.Trim();
                templateNameForLog = templateName;
                targetStepIdForLog = targetStep.Id;

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
                string provider = NormalizeProvider(context.Provider);
                string? phoneNumberId = string.IsNullOrWhiteSpace(context.PhoneNumberId) ? null : context.PhoneNumberId!.Trim();
                providerForLog = provider;
                phoneNumberIdForLog = phoneNumberId;

                if (provider != "PINNACLE" && provider != "META_CLOUD" || string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    // Pull unified settings (provider + default phone for that provider)
                    var wa = await _whatsAppSettingsService.GetSettingsByBusinessIdAsync(context.BusinessId);
                    if (wa == null)
                    {
                        var err = "No active WhatsApp settings found.";
                        _logger.LogWarning(
                            "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' providerHint={Provider}/{PhoneNumberId}",
                            err, context.BusinessId, context.FlowId, context.SourceStepId, context.TargetStepId, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, context.Provider, context.PhoneNumberId);

                        await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                        return new NextStepResult { Success = false, Error = err };
                    }

                    // Context wins if valid, else fall back to DTO
                    if (provider != "PINNACLE" && provider != "META_CLOUD")
                    {
                        var key = (wa.Provider ?? string.Empty).Trim().ToLowerInvariant();
                        provider = key switch
                        {
                            "meta" => "META_CLOUD",
                            "meta_cloud" => "META_CLOUD",
                            "meta-cloud" => "META_CLOUD",
                            "meta cloud" => "META_CLOUD",
                            "pinnacle" => "PINNACLE",
                            _ => NormalizeProvider(wa.Provider)
                        };
                        providerForLog = provider;
                    }

                    if (string.IsNullOrWhiteSpace(phoneNumberId))
                    {
                        // ESU constraint: PhoneNumberId must come ONLY from WhatsAppPhoneNumbers.
                        // Never read WhatsAppSettings.PhoneNumberId (legacy column).
                        var sender = await _whatsAppSenderService.ResolveDefaultSenderAsync(
                            context.BusinessId,
                            providerHint: provider,
                            ct: default);

                        if (!sender.Success)
                        {
                            var err = sender.Error ?? "Failed to resolve WhatsApp sender.";
                            _logger.LogWarning(
                                "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' provider={Provider}",
                                err, context.BusinessId, context.FlowId, context.SourceStepId, context.TargetStepId, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, provider);

                            await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                            return new NextStepResult { Success = false, Error = err };
                        }

                        // Keep provider stable if already known; if not known, align to the resolved sender provider.
                        if (provider != "PINNACLE" && provider != "META_CLOUD" && !string.IsNullOrWhiteSpace(sender.Provider))
                            provider = NormalizeProvider(sender.Provider);

                        phoneNumberId = sender.PhoneNumberId;
                        providerForLog = provider;
                        phoneNumberIdForLog = phoneNumberId;
                    }
                }

                if (provider != "PINNACLE" && provider != "META_CLOUD")
                {
                    var err = "No active WhatsApp sender configured (provider could not be resolved).";
                    _logger.LogWarning(
                        "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' provider={Provider}/{PhoneNumberId}",
                        err, context.BusinessId, context.FlowId, context.SourceStepId, context.TargetStepId, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, provider, phoneNumberId);

                    await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                    return new NextStepResult { Success = false, Error = err };
                }

                if (provider == "META_CLOUD" && string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    // This should only happen if WhatsAppPhoneNumbers has no active sender; log + persist to avoid silent click failures.
                    var err = "Missing PhoneNumberId (no default Meta sender configured).";
                    _logger.LogWarning(
                        "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' provider={Provider}",
                        err, context.BusinessId, context.FlowId, context.SourceStepId, context.TargetStepId, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, provider);

                    await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                    return new NextStepResult { Success = false, Error = err };
                }

                // ── BODY placeholder resolution (static step params + optional profile-name injection) ─────────
                // CTA flows run from webhook clicks and do not have campaign-time personalization context, so
                // we persist body parameter values per step (CTAFlowSteps.BodyParamsJson).
                // NOTE: TemplateMetadataDto.PlaceholderCount includes button tokens too; use WhatsAppTemplates.BodyVarCount.
                var bodyVarCount = await ResolveBodyVarCountAsync(context.BusinessId, templateName, meta);
                var args = new List<string>(Math.Max(0, bodyVarCount));
                if (bodyVarCount > 0)
                {
                    args.AddRange(Enumerable.Repeat(string.Empty, bodyVarCount));
                    var stored = TryParseBodyParams(targetStep.BodyParamsJson);
                    for (var i = 0; i < bodyVarCount && i < stored.Count; i++)
                        args[i] = (stored[i] ?? string.Empty).Trim();
                }

                if (targetStep.UseProfileName && targetStep.ProfileNameSlot is int slot && slot >= 1)
                {
                    if (bodyVarCount <= 0)
                    {
                        _logger.LogWarning(
                            "CTAFlow profile-name slot configured but template has no body vars biz={Biz} flow={Flow} step={Step} tmpl={T} slot={Slot}",
                            context.BusinessId, context.FlowId, targetStep.Id, templateName, slot);
                    }
                    else if (slot <= bodyVarCount)
                    {
                        var contact = await _dbContext.Contacts
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.BusinessId == context.BusinessId
                                                      && c.PhoneNumber == context.ContactPhone);

                        var greet = ResolveGreeting(contact?.ProfileName, contact?.Name);
                        args[slot - 1] = greet;
                    }
                }

                if (bodyVarCount > 0 && args.Any(a => string.IsNullOrWhiteSpace(a)))
                {
                    var err = $"Template '{templateName}' requires {bodyVarCount} body parameter(s), but one or more values are missing.";
                    _logger.LogWarning(
                        "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}'",
                        err, context.BusinessId, context.FlowId, context.SourceStepId, targetStep.Id, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText);

                    await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                    return new NextStepResult { Success = false, Error = err };
                }

                var components = new List<object>();

                // ── Media header support (phase 1) ─────────────────────────────────────────
                // CTA flow runtime previously sent only BODY components. For templates whose header kind is
                // image/video/document, WhatsApp (Meta Cloud) requires a header component with a media link.
                // Phase 2: HeaderMediaUrl is persisted per step (CTAFlowSteps.HeaderMediaUrl). We still accept an
                // execution-context override (e.g., campaign/runtime) and keep a temporary fallback from clicked
                // button value to avoid breaking older flows until the UI starts populating the step field.
                var headerKind = (meta.HeaderKind ?? "none").Trim().ToLowerInvariant();
                var requiresMediaHeader = headerKind is "image" or "video" or "document";
                string? headerMediaUrl = string.IsNullOrWhiteSpace(context.HeaderMediaUrl) ? null : context.HeaderMediaUrl!.Trim();

                if (requiresMediaHeader && string.IsNullOrWhiteSpace(headerMediaUrl))
                {
                    // Phase 2: Prefer persisted per-step configuration.
                    headerMediaUrl = string.IsNullOrWhiteSpace(targetStep.HeaderMediaUrl) ? null : targetStep.HeaderMediaUrl!.Trim();
                }

                if (requiresMediaHeader && string.IsNullOrWhiteSpace(headerMediaUrl))
                {
                    // Temporary fallback: treat a clicked button value that looks like a URL as the media link.
                    // This is best-effort for backward compatibility; UI should persist HeaderMediaUrl on the step.
                    if (TryGetHttpUrl(context.ClickedButton?.ButtonValue, out var fallbackUrl))
                    {
                        headerMediaUrl = fallbackUrl;
                        _logger.LogInformation(
                            "CTAFlow header media URL sourced from clicked button value biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} kind={Kind}",
                            context.BusinessId, context.FlowId, context.SourceStepId, targetStep.Id, templateName, headerKind);
                    }
                }

                if (requiresMediaHeader)
                {
                    if (!string.Equals(provider, "META_CLOUD", StringComparison.OrdinalIgnoreCase))
                    {
                        var err = $"CTAFlow media-header templates are not supported for provider '{provider}' yet.";
                        _logger.LogWarning(
                            "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' provider={Prov}",
                            err, context.BusinessId, context.FlowId, context.SourceStepId, targetStep.Id, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, provider);

                        await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                        return new NextStepResult { Success = false, Error = err };
                    }

                    if (string.IsNullOrWhiteSpace(headerMediaUrl))
                    {
                        var err = $"Template '{templateName}' requires a {headerKind} header, but no HeaderMediaUrl was provided.";
                        _logger.LogWarning(
                            "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' provider={Prov}/{Pnid}",
                            err, context.BusinessId, context.FlowId, context.SourceStepId, targetStep.Id, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, provider, phoneNumberId);

                        await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                        return new NextStepResult { Success = false, Error = err };
                    }

                    object mediaParam = headerKind switch
                    {
                        "image" => new { type = "image", image = new { link = headerMediaUrl } },
                        "video" => new { type = "video", video = new { link = headerMediaUrl } },
                        "document" => new { type = "document", document = new { link = headerMediaUrl } },
                        _ => new { type = "text", text = "" } // should not happen due to requiresMediaHeader guard
                    };

                    components.Add(new
                    {
                        type = "header",
                        parameters = new object[] { mediaParam }
                    });
                }
                // ───────────────────────────────────────────────────────────────────────────
                if (bodyVarCount > 0)
                {
                    components.Add(new
                    {
                        type = "body",
                        parameters = args.Select(a => new { type = "text", text = a ?? string.Empty }).ToList()
                    });
                }
                // ───────────────────────────────────────────────────────────────────────────

                // ── Dynamic URL button parameters (Meta Cloud) ───────────────────────────────────────────
                // WhatsApp Cloud requires "button" components when a template has dynamic URL buttons.
                // We store per-step values in CTAFlowSteps.UrlButtonParamsJson (index 0 => button index "0").
                if (string.Equals(provider, "META_CLOUD", StringComparison.OrdinalIgnoreCase) &&
                    meta.ButtonParams is { Count: > 0 })
                {
                    var storedUrlParams = TryParseUrlButtonParams(targetStep.UrlButtonParamsJson);

                    var buttons = meta.ButtonParams
                        .OrderBy(b => b.Index)
                        .Take(3)
                        .ToList();

                    foreach (var b in buttons)
                    {
                        var idx = b.Index;
                        if (idx < 0 || idx > 2) continue;

                        var isUrl =
                            string.Equals(b.Type, "URL", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(b.SubType, "url", StringComparison.OrdinalIgnoreCase);
                        if (!isUrl) continue;

                        var mask = (b.ParameterValue ?? string.Empty).Trim();
                        var isDynamic = mask.Contains("{{", StringComparison.Ordinal);
                        if (!isDynamic) continue;

                        var param = (idx < storedUrlParams.Count ? storedUrlParams[idx] : null) ?? string.Empty;
                        param = param.Trim();

                        if (string.IsNullOrWhiteSpace(param))
                        {
                            var err = $"Template '{templateName}' requires a dynamic URL parameter for button {idx + 1} ('{b.Text}').";
                            _logger.LogWarning(
                                "CTAFlow ExecuteNextAsync failed: {Error} biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' provider={Prov}/{Pnid} urlBtnIdx={UrlIdx} urlBtnText='{UrlText}'",
                                err, context.BusinessId, context.FlowId, context.SourceStepId, targetStep.Id, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, provider, phoneNumberId, idx, b.Text);

                            await RecordFailureAsync(context, stepId: targetStep.Id, stepName: templateName, error: err);
                            return new NextStepResult { Success = false, Error = err };
                        }

                        components.Add(new
                        {
                            type = "button",
                            sub_type = "url",
                            index = idx.ToString(),
                            parameters = new object[]
                            {
                                new { type = "text", text = param }
                            }
                        });
                    }
                }


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

                if (!sendResult.Success)
                {
                    // NOTE: Added so webhook-driven flows can't fail silently; DB already captures Status=Failed below.
                    _logger.LogWarning(
                        "CTAFlow send failed biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} btnText='{BtnText}' provider={Prov}/{Pnid} err={Err}",
                        context.BusinessId, context.FlowId, context.SourceStepId, targetStep.Id, templateName, context.ContactPhone, context.ButtonIndex, clickedBtnText, provider, phoneNumberId, sendResult.ErrorMessage);
                }

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
                var err = ex.Message;
                _logger.LogError(ex,
                    "CTAFlow ExecuteNextAsync exception biz={Biz} flow={Flow} srcStep={Src} targetStep={Target} tmpl={T} to={To} btnIdx={BtnIdx} provider={Provider}/{PhoneNumberId}",
                    context.BusinessId, context.FlowId, context.SourceStepId, targetStepIdForLog, templateNameForLog, context.ContactPhone, context.ButtonIndex, providerForLog, phoneNumberIdForLog);

                await RecordFailureAsync(
                    context,
                    stepId: targetStepIdForLog ?? context.SourceStepId,
                    stepName: templateNameForLog ?? "EXCEPTION",
                    error: err);

                return new NextStepResult { Success = false, Error = err };
            }
        }

        private static readonly System.Text.RegularExpressions.Regex PositionalToken =
            new(@"\{\{\s*\d+\s*\}\}", System.Text.RegularExpressions.RegexOptions.Compiled); // {{1}}, {{ 2 }}, etc.

        private static readonly System.Text.RegularExpressions.Regex NamedToken =
            new(@"\{\{\s*\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);        // {{}} (NAMED format slot)

        private static int CountBodyTokensFlexible(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return PositionalToken.Matches(text).Count + NamedToken.Matches(text).Count;
        }

        private static List<string> TryParseBodyParams(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> TryParseUrlButtonParams(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task<int> ResolveBodyVarCountAsync(Guid businessId, string templateName, xbytechat.api.WhatsAppSettings.DTOs.TemplateMetadataDto meta)
        {
            try
            {
                // WhatsAppTemplates.BodyVarCount is the canonical body placeholder count (buttons are separate).
                var count = await _dbContext.WhatsAppTemplates
                    .AsNoTracking()
                    .Where(t => t.BusinessId == businessId && t.IsActive && t.Name == templateName)
                    .OrderByDescending(t => t.UpdatedAt)
                    .Select(t => t.BodyVarCount)
                    .FirstOrDefaultAsync();

                if (count > 0) return count;
            }
            catch (Exception ex)
            {
                // Best-effort; fall back to counting tokens in the template preview body.
                _logger.LogWarning(ex,
                    "CTAFlow ResolveBodyVarCountAsync failed, falling back to token count biz={Biz} tmpl={T}",
                    businessId, templateName);
            }

            return CountBodyTokensFlexible(meta?.Body);
        }

        private static bool TryGetHttpUrl(string? maybeUrl, out string url)
        {
            url = string.Empty;
            if (string.IsNullOrWhiteSpace(maybeUrl)) return false;
            if (!Uri.TryCreate(maybeUrl.Trim(), UriKind.Absolute, out var u)) return false;
            if (!string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;
            url = u.ToString();
            return true;
        }

        private async Task RecordFailureAsync(NextStepContext context, Guid stepId, string stepName, string error)
        {
            try
            {
                _dbContext.FlowExecutionLogs.Add(new FlowExecutionLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = context.BusinessId,
                    FlowId = context.FlowId,
                    StepId = stepId,
                    StepName = stepName,
                    MessageLogId = context.MessageLogId == Guid.Empty ? null : context.MessageLogId,
                    ButtonIndex = context.ButtonIndex,
                    ContactPhone = context.ContactPhone,
                    Success = false,
                    ErrorMessage = error,
                    ExecutedAt = DateTime.UtcNow,
                    RequestId = context.RequestId
                });

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // NOTE: Do not throw from webhook runtime; log only (best-effort observability).
                _logger.LogError(ex,
                    "CTAFlow RecordFailureAsync failed biz={Biz} flow={Flow} step={Step} to={To} error={Err}",
                    context.BusinessId, context.FlowId, stepId, context.ContactPhone, error);
            }
        }


    }
}


