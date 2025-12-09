// 📄 xbytechat-api/Features/CTAFlowBuilder/Services/CtaFlowRuntimeService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api; // 👈 Keep this if AppDbContext is in the root namespace
// If AppDbContext lives under xbytechat.api.Data, then use:
// using xbytechat.api.Data;

using xbytechat.api.Features.CTAFlowBuilder.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Enums;
using xbytechat.api.Features.MessagesEngine.Services;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    /// <summary>
    /// Minimal CTA flow runtime engine (v1).
    ///
    /// Responsibilities:
    /// - Load CTAFlowConfig + steps from DB.
    /// - Execute the first step (template send) using IMessageEngineService.
    /// - Log the execution into FlowExecutionLogs via IFlowExecutionLogger.
    ///
    /// It uses FlowExecutionOrigin + AutoReplyFlowId / CampaignId so analytics
    /// can later separate:
    ///   - "CTA flow started by AutoReply"
    ///   - "CTA flow started by Campaign button"
    ///   - other origins (JourneyBot, Inbox, System).
    /// </summary>
    public sealed class CtaFlowRuntimeService : ICtaFlowRuntimeService
    {
        private readonly AppDbContext _db;
        private readonly IMessageEngineService _messageEngine;
        private readonly IFlowExecutionLogger _flowLogger;
        private readonly ILogger<CtaFlowRuntimeService> _logger;

        public CtaFlowRuntimeService(
            AppDbContext db,
            IMessageEngineService messageEngine,
            IFlowExecutionLogger flowLogger,
            ILogger<CtaFlowRuntimeService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _messageEngine = messageEngine ?? throw new ArgumentNullException(nameof(messageEngine));
            _flowLogger = flowLogger ?? throw new ArgumentNullException(nameof(flowLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CtaFlowRunResult> StartFlowAsync(
            Guid businessId,
            Guid contactId,
            string contactPhone,
            Guid configId,
            FlowExecutionOrigin origin,
            Guid? autoReplyFlowId,
            CancellationToken cancellationToken = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("businessId is required", nameof(businessId));
            if (configId == Guid.Empty)
                throw new ArgumentException("configId is required", nameof(configId));
            if (string.IsNullOrWhiteSpace(contactPhone))
                throw new ArgumentException("contactPhone is required", nameof(contactPhone));

            _logger.LogInformation(
                "🚀 [CTAFlowRuntime] StartFlowAsync Biz={BusinessId}, Contact={ContactId}, Phone={Phone}, Config={ConfigId}, Origin={Origin}, AutoReplyFlowId={AutoReplyFlowId}",
                businessId,
                contactId,
                contactPhone,
                configId,
                origin,
                autoReplyFlowId);

            // 1️⃣ Load the CTA flow (must be active + published) with its steps
            var flow = await _db.CTAFlowConfigs
                .AsNoTracking()
                .Include(f => f.Steps)
                .FirstOrDefaultAsync(
                    f => f.Id == configId
                         && f.BusinessId == businessId
                         && f.IsActive
                         && f.IsPublished,
                    cancellationToken);

            if (flow == null)
            {
                var message =
                    $"CTA flow {configId} for business {businessId} not found, inactive, or not published.";

                _logger.LogWarning("[CTAFlowRuntime] {Message}", message);

                // Log a failed "meta-step" so analytics can see the failure
                var failCtx = new FlowExecutionContext
                {
                    BusinessId = businessId,
                    FlowId = configId,
                    AutoReplyFlowId = autoReplyFlowId,
                    Origin = origin,

                    // NEW: log which contact we tried to start for
                    ContactId = contactId,
                    ContactPhone = contactPhone,

                    StepId = configId,          // no specific step; use flow id as placeholder
                    StepName = "FLOW_NOT_FOUND",

                    // No template here
                    TemplateName = null,
                    TemplateType = null,

                    Success = false,
                    ErrorMessage = message,
                    ExecutedAtUtc = DateTime.UtcNow
                };

                await _flowLogger.LogStepAsync(failCtx, cancellationToken);

                return new CtaFlowRunResult
                {
                    Success = false,
                    ErrorMessage = message
                };
            }

            // 2️⃣ Pick the first step (v1 = simple linear flow)
            var firstStep = flow.Steps
                .OrderBy(s => s.StepOrder)
                .FirstOrDefault();

            if (firstStep == null)
            {
                var message =
                    $"CTA flow {flow.Id} ('{flow.FlowName}') has no steps configured.";

                _logger.LogWarning("[CTAFlowRuntime] {Message}", message);

                var failCtx = new FlowExecutionContext
                {
                    BusinessId = businessId,
                    FlowId = flow.Id,
                    AutoReplyFlowId = autoReplyFlowId,
                    Origin = origin,

                    // NEW: log contact context even on failure
                    ContactId = contactId,
                    ContactPhone = contactPhone,

                    StepId = flow.Id,
                    StepName = "NO_STEPS",

                    TemplateName = null,
                    TemplateType = null,

                    Success = false,
                    ErrorMessage = message,
                    ExecutedAtUtc = DateTime.UtcNow
                };

                await _flowLogger.LogStepAsync(failCtx, cancellationToken);

                return new CtaFlowRunResult
                {
                    Success = false,
                    ErrorMessage = message
                };
            }

            _logger.LogInformation(
                "[CTAFlowRuntime] Executing first step {StepId} ({Template}) of flow {FlowId} ('{FlowName}')",
                firstStep.Id,
                firstStep.TemplateToSend,
                flow.Id,
                flow.FlowName);

            // 3️⃣ Build SimpleTemplateMessageDto with CTA tracking fields
            var templateDto = new SimpleTemplateMessageDto
            {
                RecipientNumber = contactPhone,
                TemplateName = firstStep.TemplateToSend,

                // v1: no dynamic params here – flows can be extended later
                TemplateParameters = new List<string>(),

                // v1: let MessageEngine choose routing / provider
                HasStaticButtons = false,
                Provider = string.Empty,
                PhoneNumberId = null,

                // 🔗 Link back to CTA flow config + step
                CTAFlowConfigId = flow.Id,
                CTAFlowStepId = firstStep.Id,

                // Optional fields – keep null for now
                TemplateBody = null,
                LanguageCode = null
            };

            // 4️⃣ Send the message via MessageEngine (conversational → Immediate)
            var sendResult = await _messageEngine
                .SendTemplateMessageSimpleAsync(
                    businessId,
                    templateDto,
                    DeliveryMode.Immediate);

            // 5️⃣ Log the step into FlowExecutionLogs
            var logCtx = new FlowExecutionContext
            {
                BusinessId = businessId,
                FlowId = flow.Id,
                AutoReplyFlowId = autoReplyFlowId,
                Origin = origin,

                // Contact context
                ContactId = contactId,
                ContactPhone = contactPhone,

                // Step context
                StepId = firstStep.Id,
                StepName = firstStep.TemplateToSend,

                // Template metadata for analytics
                TemplateName = firstStep.TemplateToSend,
                TemplateType = firstStep.TemplateType ?? "CTA_FLOW_TEMPLATE",

                // Result
                Success = sendResult.Success,
                ErrorMessage = sendResult.Success ? null : sendResult.Message,
                ExecutedAtUtc = DateTime.UtcNow

                // MessageLogId, CatalogClickLogId, CampaignId, etc.
                // can be wired later once message engine returns those ids.
            };

            await _flowLogger.LogStepAsync(logCtx, cancellationToken);

            return new CtaFlowRunResult
            {
                Success = sendResult.Success,
                ErrorMessage = sendResult.Success ? null : sendResult.Message
            };
        }
    }
}


//// 📄 xbytechat-api/Features/CTAFlowBuilder/Services/CtaFlowRuntimeService.cs
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using xbytechat.api; // 👈 Keep this if AppDbContext is in the root namespace
//// If AppDbContext lives under xbytechat.api.Data, then use:
//// using xbytechat.api.Data;

//using xbytechat.api.Features.CTAFlowBuilder.DTOs;
//using xbytechat.api.Features.CTAFlowBuilder.Models;
//using xbytechat.api.Features.MessagesEngine.DTOs;
//using xbytechat.api.Features.MessagesEngine.Services;

//namespace xbytechat.api.Features.CTAFlowBuilder.Services
//{
//    /// <summary>
//    /// Minimal CTA flow runtime engine (v1).
//    ///
//    /// Responsibilities:
//    /// - Load CTAFlowConfig + steps from DB.
//    /// - Execute the first step (template send) using IMessageEngineService.
//    /// - Log the execution into FlowExecutionLogs via IFlowExecutionLogger.
//    ///
//    /// It uses FlowExecutionOrigin + AutoReplyFlowId / CampaignId so analytics
//    /// can later separate:
//    ///   - "CTA flow started by AutoReply"
//    ///   - "CTA flow started by Campaign button"
//    ///   - other origins (JourneyBot, Inbox, System).
//    /// </summary>
//    public sealed class CtaFlowRuntimeService : ICtaFlowRuntimeService
//    {
//        private readonly AppDbContext _db;
//        private readonly IMessageEngineService _messageEngine;
//        private readonly IFlowExecutionLogger _flowLogger;
//        private readonly ILogger<CtaFlowRuntimeService> _logger;

//        public CtaFlowRuntimeService(
//            AppDbContext db,
//            IMessageEngineService messageEngine,
//            IFlowExecutionLogger flowLogger,
//            ILogger<CtaFlowRuntimeService> logger)
//        {
//            _db = db ?? throw new ArgumentNullException(nameof(db));
//            _messageEngine = messageEngine ?? throw new ArgumentNullException(nameof(messageEngine));
//            _flowLogger = flowLogger ?? throw new ArgumentNullException(nameof(flowLogger));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public async Task<CtaFlowRunResult> StartFlowAsync(
//            Guid businessId,
//            Guid contactId,
//            string contactPhone,
//            Guid configId,
//            FlowExecutionOrigin origin,
//            Guid? autoReplyFlowId,
//            CancellationToken cancellationToken = default)
//        {
//            if (businessId == Guid.Empty)
//                throw new ArgumentException("businessId is required", nameof(businessId));
//            if (configId == Guid.Empty)
//                throw new ArgumentException("configId is required", nameof(configId));
//            if (string.IsNullOrWhiteSpace(contactPhone))
//                throw new ArgumentException("contactPhone is required", nameof(contactPhone));

//            _logger.LogInformation(
//                "🚀 [CTAFlowRuntime] StartFlowAsync Biz={BusinessId}, Contact={ContactId}, Phone={Phone}, Config={ConfigId}, Origin={Origin}, AutoReplyFlowId={AutoReplyFlowId}",
//                businessId,
//                contactId,
//                contactPhone,
//                configId,
//                origin,
//                autoReplyFlowId);

//            // 1️⃣ Load the CTA flow (must be active + published) with its steps
//            var flow = await _db.CTAFlowConfigs
//                .AsNoTracking()
//                .Include(f => f.Steps)
//                .FirstOrDefaultAsync(
//                    f => f.Id == configId
//                         && f.BusinessId == businessId
//                         && f.IsActive
//                         && f.IsPublished,
//                    cancellationToken);

//            if (flow == null)
//            {
//                var message =
//                    $"CTA flow {configId} for business {businessId} not found, inactive, or not published.";

//                _logger.LogWarning("[CTAFlowRuntime] {Message}", message);

//                // Log a failed "meta-step" so analytics can see the failure
//                var failCtx = new FlowExecutionContext
//                {
//                    BusinessId = businessId,
//                    FlowId = configId,
//                    AutoReplyFlowId = autoReplyFlowId,
//                    Origin = origin,

//                    // NEW: log which contact we tried to start for
//                    ContactId = contactId,
//                    ContactPhone = contactPhone,

//                    StepId = configId,          // no specific step; use flow id as placeholder
//                    StepName = "FLOW_NOT_FOUND",

//                    // No template here
//                    TemplateName = null,
//                    TemplateType = null,

//                    Success = false,
//                    ErrorMessage = message,
//                    ExecutedAtUtc = DateTime.UtcNow
//                };

//                await _flowLogger.LogStepAsync(failCtx, cancellationToken);

//                return new CtaFlowRunResult
//                {
//                    Success = false,
//                    ErrorMessage = message
//                };
//            }

//            // 2️⃣ Pick the first step (v1 = simple linear flow)
//            var firstStep = flow.Steps
//                .OrderBy(s => s.StepOrder)
//                .FirstOrDefault();

//            if (firstStep == null)
//            {
//                var message =
//                    $"CTA flow {flow.Id} ('{flow.FlowName}') has no steps configured.";

//                _logger.LogWarning("[CTAFlowRuntime] {Message}", message);

//                var failCtx = new FlowExecutionContext
//                {
//                    BusinessId = businessId,
//                    FlowId = flow.Id,
//                    AutoReplyFlowId = autoReplyFlowId,
//                    Origin = origin,

//                    // NEW: log contact context even on failure
//                    ContactId = contactId,
//                    ContactPhone = contactPhone,

//                    StepId = flow.Id,
//                    StepName = "NO_STEPS",

//                    TemplateName = null,
//                    TemplateType = null,

//                    Success = false,
//                    ErrorMessage = message,
//                    ExecutedAtUtc = DateTime.UtcNow
//                };

//                await _flowLogger.LogStepAsync(failCtx, cancellationToken);

//                return new CtaFlowRunResult
//                {
//                    Success = false,
//                    ErrorMessage = message
//                };
//            }

//            _logger.LogInformation(
//                "[CTAFlowRuntime] Executing first step {StepId} ({Template}) of flow {FlowId} ('{FlowName}')",
//                firstStep.Id,
//                firstStep.TemplateToSend,
//                flow.Id,
//                flow.FlowName);

//            // 3️⃣ Build SimpleTemplateMessageDto with CTA tracking fields
//            var templateDto = new SimpleTemplateMessageDto
//            {
//                RecipientNumber = contactPhone,
//                TemplateName = firstStep.TemplateToSend,

//                // v1: no dynamic params here – flows can be extended later
//                TemplateParameters = new List<string>(),

//                // v1: let MessageEngine choose routing / provider
//                HasStaticButtons = false,
//                Provider = string.Empty,
//                PhoneNumberId = null,

//                // 🔗 Link back to CTA flow config + step
//                CTAFlowConfigId = flow.Id,
//                CTAFlowStepId = firstStep.Id,

//                // Optional fields – keep null for now
//                TemplateBody = null,
//                LanguageCode = null
//            };

//            // 4️⃣ Send the message via MessageEngine
//            var sendResult = await _messageEngine
//                .SendTemplateMessageSimpleAsync(businessId, templateDto);

//            // 5️⃣ Log the step into FlowExecutionLogs
//            var logCtx = new FlowExecutionContext
//            {
//                BusinessId = businessId,
//                FlowId = flow.Id,
//                AutoReplyFlowId = autoReplyFlowId,
//                Origin = origin,

//                // Contact context
//                ContactId = contactId,
//                ContactPhone = contactPhone,

//                // Step context
//                StepId = firstStep.Id,
//                StepName = firstStep.TemplateToSend,

//                // Template metadata for analytics
//                TemplateName = firstStep.TemplateToSend,
//                TemplateType = firstStep.TemplateType ?? "CTA_FLOW_TEMPLATE",

//                // Result
//                Success = sendResult.Success,
//                ErrorMessage = sendResult.Success ? null : sendResult.Message,
//                ExecutedAtUtc = DateTime.UtcNow

//                // MessageLogId, CatalogClickLogId, CampaignId, etc.
//                // can be wired later once message engine returns those ids.
//            };

//            await _flowLogger.LogStepAsync(logCtx, cancellationToken);

//            return new CtaFlowRunResult
//            {
//                Success = sendResult.Success,
//                ErrorMessage = sendResult.Success ? null : sendResult.Message
//            };
//        }
//    }
//}


