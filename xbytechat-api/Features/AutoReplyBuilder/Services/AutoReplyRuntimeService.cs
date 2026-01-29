using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;
using xbytechat.api.Features.AutoReplyBuilder.Models;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Services;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using System.Text.Json.Serialization;
using xbytechat.api.Features.MessagesEngine.Enums;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.WhatsAppSettings.DTOs;

namespace xbytechat.api.Features.AutoReplyBuilder.Services
{
    /// <summary>
    /// Default implementation of the AutoReply runtime engine.
    ///
    /// Responsibilities:
    /// - Match inbound text against AutoReplyFlow.TriggerKeyword (builder flows).
    /// - Provide:
    ///     * Test-only matching for the AutoReplyBuilder UI (no sending).
    ///     * Runtime matching for the WhatsApp webhook (executes multi-step flows).
    ///     * Legacy DTO-based matching for existing APIs (FindMatchAsync).
    /// - For builder flows, can inspect NodesJson and execute "message" / "template" /
    ///   "tag" / "wait" nodes in order (simple linear runner for now).
    /// </summary>
    public sealed class AutoReplyRuntimeService : IAutoReplyRuntimeService
    {
        private readonly AppDbContext _dbContext;
        private readonly IMessageEngineService _messageEngine;
        private readonly ILogger<AutoReplyRuntimeService> _logger;
        private readonly IFlowExecutionLogger _flowExecutionLogger;
        private readonly ICtaFlowRuntimeService _ctaFlowRuntime;

        public AutoReplyRuntimeService(
            AppDbContext dbContext,
            IMessageEngineService messageEngine,
            ILogger<AutoReplyRuntimeService> logger,
            IFlowExecutionLogger flowExecutionLogger,
            ICtaFlowRuntimeService ctaFlowRuntime)
        {
            _dbContext = dbContext;
            _messageEngine = messageEngine;
            _logger = logger;
            _flowExecutionLogger = flowExecutionLogger;
            _ctaFlowRuntime = ctaFlowRuntime;
        }

        // ----------------------------------------------------
        // 1) Runtime – used by the webhook
        // ----------------------------------------------------

        public async Task<AutoReplyRuntimeResult> TryHandleAsync(
            Guid businessId,
            Guid contactId,
            string contactPhone,
            string incomingText,
            CancellationToken cancellationToken = default)
        {
            var text = (incomingText ?? string.Empty).Trim();

            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(text))
            {
                return AutoReplyRuntimeResult.NotHandled(
                    "BusinessId was empty or incoming text was blank (runtime).");
            }

            _logger.LogInformation(
                "🤖 AutoReplyRuntime invoked (runtime) for Business={BusinessId}, Contact={ContactId}, Phone={Phone}, Text={Text}",
                businessId,
                contactId,
                contactPhone,
                text);

            // Reuse the canonical matching logic (builder flows only)
            var matchResult = await TestMatchAsync(businessId, text, cancellationToken);

            // ⛔ Only short-circuit when we have NO builder flow id at all.
            // If AutoReplyFlowId is present, we still try to execute the flow
            // even if Handled == false.
            if (!matchResult.Handled && !matchResult.AutoReplyFlowId.HasValue)
            {
                _logger.LogDebug(
                    "👂 AutoReplyRuntime (runtime) found no matching visual flow for Business={BusinessId}",
                    businessId);

                return matchResult;
            }

            _logger.LogInformation(
                "🤖 AutoReplyRuntime (runtime) matched flow {FlowId} with keyword '{Keyword}'. Notes: {Notes}",
                matchResult.AutoReplyFlowId ?? matchResult.CtaFlowConfigId,
                matchResult.MatchedKeyword,
                matchResult.Notes);

            // New behaviour: execute the full visual builder flow as a linear sequence.
            if (matchResult.AutoReplyFlowId.HasValue)
            {
                var flowId = matchResult.AutoReplyFlowId.Value;

                var flow = await _dbContext.Set<AutoReplyFlow>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        f => f.Id == flowId && f.BusinessId == businessId,
                        cancellationToken);

                if (flow == null)
                {
                    _logger.LogWarning(
                        "AutoReplyRuntime matched flow id {FlowId} but could not reload AutoReplyFlow from DB.",
                        flowId);

                    matchResult.SentSimpleReply = false;
                    // Keep original Handled flag; webhook can decide what to do.
                    return matchResult;
                }

                // Extra visibility for debugging
                var matchMode = string.IsNullOrWhiteSpace(flow.MatchMode) ? "Word" : flow.MatchMode;
                _logger.LogInformation(
                    "🤖 AutoReplyRuntime (runtime) executing flow {FlowId} '{FlowName}' for Business={BusinessId}, Contact={ContactId}. Mode={MatchMode}, Priority={Priority}, Keyword='{Keyword}'",
                    flow.Id,
                    flow.Name,
                    businessId,
                    contactId,
                    matchMode,
                    flow.Priority,
                    matchResult.MatchedKeyword);

                var outcome = await ExecuteFlowLinearAsync(
                    flow,
                    businessId,
                    contactId,
                    contactPhone,
                    cancellationToken);

                // Mark as “we responded” only if at least one step sent something.
                matchResult.SentSimpleReply = outcome.AnySent;
                if (outcome.AnySent)
                {
                    // Force Handled = true so webhook does NOT fall back
                    // to the legacy AutomationService greeting.
                    matchResult.Handled = true;
                }

                if (!outcome.AnySent)
                {
                    _logger.LogWarning(
                        "AutoReplyRuntime executed flow {FlowId} for Business={BusinessId} but no messages/templates were sent.",
                        flowId,
                        businessId);

                    return matchResult;
                }

                _logger.LogInformation(
                    "✅ AutoReplyRuntime executed flow {FlowId} for Business={BusinessId}, Contact={ContactId}. Summary={Summary}",
                    flowId,
                    businessId,
                    contactId,
                    outcome.Summary ?? "(no summary)");

                // 📝 Always log the trigger (match + executed steps)
                await LogAutoReplyAsync(
                    businessId,
                    contactId,
                    matchResult.MatchedKeyword,
                    flow,
                    outcome.Summary,
                    messageLogId: null, // we can wire a real MessageLogId later
                    ct: cancellationToken);

                return matchResult;
            }

            // In future we can support direct CTA-flow start via matchResult.CtaFlowConfigId.
            return matchResult;
        }


        // ----------------------------------------------------
        // 2) Test mode – used by AutoReplyBuilder "Test Match"
        // ----------------------------------------------------
        public async Task<AutoReplyRuntimeResult> TestMatchAsync(
            Guid businessId,
            string incomingText,
            CancellationToken cancellationToken = default)
        {
            var text = (incomingText ?? string.Empty).Trim();

            if (businessId == Guid.Empty || string.IsNullOrWhiteSpace(text))
            {
                return AutoReplyRuntimeResult.NotHandled(
                    "BusinessId was empty or incoming text was blank (test).");
            }

            _logger.LogDebug(
                "🧪 AutoReplyRuntime test-match for Business={BusinessId}, Text={Text}",
                businessId,
                text);

            var normalizedText = text.ToLowerInvariant();
            var incomingWords = SplitIntoWords(normalizedText);

            // 2.1) Builder flows (new AutoReplyBuilder)
            var flows = await _dbContext.Set<AutoReplyFlow>()
                .AsNoTracking()
                .Where(f =>
                    f.BusinessId == businessId &&
                    f.IsActive &&
                    !string.IsNullOrWhiteSpace(f.TriggerKeyword))
                .OrderBy(f => f.Name)
                .ThenBy(f => f.Id)
                .ToListAsync(cancellationToken);

            var candidates = new List<FlowMatchCandidate>();

            foreach (var flow in flows)
            {
                var triggerField = flow.TriggerKeyword ?? string.Empty;

                // Support comma / newline separated triggers
                var keywords = triggerField
                    .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .ToList();

                if (keywords.Count == 0)
                    continue;

                var rawMatchMode = string.IsNullOrWhiteSpace(flow.MatchMode)
                    ? "Word"
                    : flow.MatchMode.Trim();

                var matchMode = rawMatchMode.ToUpperInvariant();

                foreach (var kw in keywords)
                {
                    var normalizedKeyword = kw.ToLowerInvariant();

                    if (IsKeywordMatch(
                            normalizedKeyword,
                            normalizedText,
                            matchMode,
                            incomingWords))
                    {
                        candidates.Add(new FlowMatchCandidate
                        {
                            Flow = flow,
                            Keyword = kw,
                            MatchMode = matchMode,
                            Priority = flow.Priority,
                            KeywordLength = normalizedKeyword.Length
                        });
                    }
                }
            }

            if (candidates.Count == 0)
            {
                // ✅ No legacy rule fallback anymore: if no flow matches, we just say "not handled".
                return AutoReplyRuntimeResult.NotHandled(
                    "No AutoReply flow matched the incoming text.");
            }

            // 🎯 Big-player style selection:
            // 1) Highest Priority
            // 2) Longest keyword (more specific)
            // 3) Newest flow (CreatedAt)
            // 4) Stable by Id
            var winner = candidates
                .OrderByDescending(c => c.Priority)
                .ThenByDescending(c => c.KeywordLength)
                .ThenByDescending(c => c.Flow.CreatedAt)
                .ThenBy(c => c.Flow.Id)
                .First();

            var matchedFlow = winner.Flow;
            var matchedKeyword = winner.Keyword;

            var startSummary = GetFirstActionNodeSummary(matchedFlow);

            var note =
                $"Matched flow '{matchedFlow.Name}' ({matchedFlow.Id}) by trigger '{matchedKeyword}'. " +
                $"Mode={winner.MatchMode}, Priority={winner.Priority}, Candidates={candidates.Count}.";

            _logger.LogInformation(
                "🧪 AutoReplyRuntime (test) matched winner flow {FlowId} '{FlowName}' with keyword '{Keyword}'. Mode={MatchMode}, Priority={Priority}, Candidates={CandidateCount}.",
                matchedFlow.Id,
                matchedFlow.Name,
                matchedKeyword,
                winner.MatchMode,
                winner.Priority,
                candidates.Count);

            // Still treat as "simple reply" from the point of view of the test API,
            // even though runtime now executes multi-step flows.
            return AutoReplyRuntimeResult.SimpleReply(
                autoReplyFlowId: matchedFlow.Id,
                matchedKeyword: matchedKeyword,
                notes: note,
                startNodeType: startSummary?.NodeType,
                startNodeName: startSummary?.NodeName
            );
        }

        // ----------------------------------------------------
        // 3) Legacy DTO adapter – for existing APIs
        // ----------------------------------------------------
        public async Task<AutoReplyMatchResultDto> FindMatchAsync(
            AutoReplyMatchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var runtimeResult = await TestMatchAsync(
                request.BusinessId,
                request.IncomingText,
                cancellationToken);

            if (!runtimeResult.Handled)
            {
                // Back-compat: previously returned IsMatch=false with everything else null.
                return new AutoReplyMatchResultDto
                {
                    IsMatch = false
                };
            }

            var dto = new AutoReplyMatchResultDto
            {
                IsMatch = true,
                FlowId = runtimeResult.AutoReplyFlowId ?? runtimeResult.CtaFlowConfigId,
                FlowName = null, // TODO: surface flow name once we propagate it from flow entity.
                MatchedKeyword = runtimeResult.MatchedKeyword,
                MatchType = runtimeResult.StartedCtaFlow ? "CTA_FLOW" : "SIMPLE_REPLY",
                StartNodeId = null,
                StartNodeType = null,
                StartNodeConfigJson = null
            };

            return dto;
        }

        // ----------------------------------------------------
        // 4) Multi-step flow execution (builder flows)
        // ----------------------------------------------------

        /// <summary>
        /// Represents the outcome of executing a visual AutoReply flow.
        /// </summary>
        private sealed class FlowExecutionOutcome
        {
            /// <summary>
            /// True if at least one message/template was sent.
            /// </summary>
            public bool AnySent { get; set; }

            /// <summary>
            /// Number of plain text messages sent in this run.
            /// </summary>
            public int SentTextMessages { get; set; }

            /// <summary>
            /// Number of template messages sent in this run.
            /// </summary>
            public int SentTemplates { get; set; }

            /// <summary>
            /// Extra notes about execution (e.g. errors, loop guards, tags/waits summary).
            /// </summary>
            public string? Notes { get; set; }

            /// <summary>
            /// Short human-readable summary for logs.
            /// </summary>
            public string? Summary
            {
                get
                {
                    var parts = new List<string>();

                    if (SentTextMessages > 0)
                    {
                        parts.Add($"Text x{SentTextMessages}");
                    }

                    if (SentTemplates > 0)
                    {
                        parts.Add($"Template x{SentTemplates}");
                    }

                    if (!string.IsNullOrWhiteSpace(Notes))
                    {
                        parts.Add(Notes!);
                    }

                    return parts.Count == 0 ? null : string.Join(" | ", parts);
                }
            }

            public static FlowExecutionOutcome Empty { get; } = new();
        }

        /// <summary>
        /// Execute a builder-based flow as a simple linear sequence of nodes.
        /// For now:
        /// - Supports: "message", "template", "wait", "tag".
        /// - Executes in the order defined by node.Order, ignoring the visual "start" node.
        /// </summary>
        private async Task<FlowExecutionOutcome> ExecuteFlowLinearAsync(
            AutoReplyFlow flow,
            Guid businessId,
            Guid contactId,
            string contactPhone,
            CancellationToken ct)
        {
            var nodes = DeserializeNodes(flow.NodesJson);
            if (nodes == null || nodes.Count == 0)
            {
                _logger.LogWarning(
                    "AutoReply flow {FlowId} for Business={BusinessId} has no nodes; nothing to execute.",
                    flow.Id,
                    businessId);

                return FlowExecutionOutcome.Empty;
            }

            // For now we treat flows as linear sequences:
            // - ignore the explicit edges graph
            // - skip the visual "start" node
            var orderedNodes = nodes
                .Where(n => !string.Equals(n.NodeType, "start", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Order)
                .ToList();

            if (orderedNodes.Count == 0)
            {
                _logger.LogWarning(
                    "AutoReply flow {FlowId} for Business={BusinessId} has only a start node and no action nodes.",
                    flow.Id,
                    businessId);

                return FlowExecutionOutcome.Empty;
            }

            var outcome = new FlowExecutionOutcome();
            var pieces = new List<string>();

            foreach (var node in orderedNodes)
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "AutoReply flow execution cancelled for FlowId={FlowId}, Business={BusinessId}, Contact={ContactId}.",
                        flow.Id,
                        businessId,
                        contactId);
                    break;
                }

                var nodeType = node.NodeType?.Trim().ToLowerInvariant();
                AutoReplyNodeConfig? cfg = null;

                if (!string.IsNullOrWhiteSpace(node.ConfigJson))
                {
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        cfg = JsonSerializer.Deserialize<AutoReplyNodeConfig>(node.ConfigJson, options);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to parse ConfigJson for AutoReply node {NodeId} in flow {FlowId}. ConfigJson={ConfigJson}",
                            node.Id,
                            flow.Id,
                            node.ConfigJson);

                        // Log as failed step and continue
                        await LogFlowStepAsync(
                            businessId,
                            flow,
                            node,
                            contactPhone,
                            messageLogId: null,
                            success: false,
                            errorMessage: "ConfigJson deserialization failed",
                            templateName: null,
                            templateType: null,
                            cancellationToken: ct);

                        continue;
                    }
                }

                switch (nodeType)
                {
                    case "message":
                        {
                            var body = cfg?.Text ?? cfg?.Body;
                            if (string.IsNullOrWhiteSpace(body))
                            {
                                _logger.LogWarning(
                                    "AutoReply message node {NodeId} in flow {FlowId} has empty body.",
                                    node.Id,
                                    flow.Id);

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: "Empty message body",
                                    templateName: null,
                                    templateType: "AUTO_REPLY_TEXT",
                                    cancellationToken: ct);

                                break;
                            }

                            var trimmedBody = body.Trim();

                            _logger.LogInformation(
                                "📄 AutoReplyRuntime sending text message node {NodeId} for flow {FlowId}.",
                                node.Id,
                                flow.Id);

                            // ✅ Now use DeliveryMode.Immediate (conversational reply)
                            var sendResult = await _messageEngine.SendAutoReplyTextAsync(
                                businessId,
                                contactPhone,
                                trimmedBody,
                                DeliveryMode.Immediate,
                                ct);

                            if (!sendResult.Success)
                            {
                                _logger.LogWarning(
                                    "❌ AutoReplyRuntime failed to send text node {NodeId} in flow {FlowId}, Business={BusinessId}, Contact={ContactId}: {Message}",
                                    node.Id,
                                    flow.Id,
                                    businessId,
                                    contactId,
                                    sendResult.Message);

                                outcome.Notes = $"Failed to send text node {node.Id}: {sendResult.Message}";

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null, // TODO: wire real MessageLogId from sendResult if available
                                    success: false,
                                    errorMessage: sendResult.Message,
                                    templateName: null,
                                    templateType: "AUTO_REPLY_TEXT",
                                    cancellationToken: ct);

                                return outcome;
                            }

                            outcome.AnySent = true;
                            outcome.SentTextMessages++;
                            pieces.Add(trimmedBody);

                            await LogFlowStepAsync(
                                businessId,
                                flow,
                                node,
                                contactPhone,
                                messageLogId: null, // TODO: from sendResult
                                success: true,
                                errorMessage: null,
                                templateName: null,
                                templateType: "AUTO_REPLY_TEXT",
                                cancellationToken: ct);

                            break;
                        }

                    case "template":
                        {
                            var templateName = cfg?.TemplateName;
                            if (string.IsNullOrWhiteSpace(templateName))
                            {
                                _logger.LogWarning(
                                    "AutoReply template node {NodeId} in flow {FlowId} has no TemplateName configured.",
                                    node.Id,
                                    flow.Id);

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: "Missing TemplateName",
                                    templateName: null,
                                    templateType: "AUTO_REPLY_TEMPLATE",
                                    cancellationToken: ct);

                                break;
                            }

                            // Lookup template meta to validate header/buttons & keep language in sync.
                            var templateRow = await _dbContext.Set<WhatsAppTemplate>()
                                .AsNoTracking()
                                .Where(t => t.BusinessId == businessId && t.IsActive && t.Name == templateName)
                                .OrderByDescending(t => t.UpdatedAt)
                                .FirstOrDefaultAsync(ct);

                            var headerKind = (templateRow?.HeaderKind ?? "none").Trim().ToLowerInvariant();
                            var languageCode = string.IsNullOrWhiteSpace(templateRow?.LanguageCode)
                                ? "en_US"
                                : templateRow!.LanguageCode!.Trim();

                            // Body params ({{1}}, {{2}}, ...). Keep the list length stable.
                            var rawBodyParams = cfg?.BodyParams ?? cfg?.Placeholders ?? new List<string>();
                            var bodyParams = (rawBodyParams ?? new List<string>())
                                .Select(p => p ?? string.Empty)
                                .ToList();

                            var expectedBodyVars = templateRow != null
                                ? Math.Max(templateRow.BodyVarCount, 0)
                                : bodyParams.Count;

                            if (expectedBodyVars > 0)
                            {
                                if (bodyParams.Count < expectedBodyVars)
                                {
                                    bodyParams.AddRange(Enumerable.Repeat(string.Empty, expectedBodyVars - bodyParams.Count));
                                }
                                else if (bodyParams.Count > expectedBodyVars)
                                {
                                    bodyParams = bodyParams.Take(expectedBodyVars).ToList();
                                }
                            }
                            else
                            {
                                // Template expects no body params; avoid sending extras.
                                bodyParams = new List<string>();
                            }

                            // Optional: auto-fill one slot from contact profile/name
                            if (cfg?.UseProfileName == true && bodyParams.Count > 0)
                            {
                                var slot = cfg.ProfileNameSlot ?? 1;
                                slot = Math.Max(1, Math.Min(slot, bodyParams.Count));

                                var contact = await _dbContext.Set<Contact>()
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(
                                        c => c.Id == contactId && c.BusinessId == businessId,
                                        ct);

                                var displayName = (contact?.ProfileName ?? contact?.Name ?? string.Empty).Trim();
                                if (string.IsNullOrWhiteSpace(displayName))
                                    displayName = "there";

                                bodyParams[slot - 1] = displayName;
                            }

                            // Header media URL (for image/video/document templates)
                            var headerMediaUrl = (cfg?.HeaderMediaUrl ?? string.Empty).Trim();
                            var needsHeaderUrl =
                                headerKind == "image" || headerKind == "video" || headerKind == "document";

                            if (needsHeaderUrl && !IsValidHttpsUrl(headerMediaUrl))
                            {
                                var msg = $"Missing/invalid HTTPS header URL for {headerKind} template '{templateName}'.";
                                _logger.LogWarning(
                                    "AutoReply template node {NodeId} in flow {FlowId} cannot send: {Message}",
                                    node.Id,
                                    flow.Id,
                                    msg);

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: msg,
                                    templateName: templateName,
                                    templateType: "AUTO_REPLY_TEMPLATE",
                                    cancellationToken: ct);

                                break;
                            }

                            // Dynamic URL button params (index 0..2) - validate required ones using cached template buttons.
                            var rawUrlParams = cfg?.UrlButtonParams ?? new List<string>();
                            var urlParams = new List<string>(capacity: 3)
                            {
                                rawUrlParams.Count > 0 ? (rawUrlParams[0] ?? string.Empty) : string.Empty,
                                rawUrlParams.Count > 1 ? (rawUrlParams[1] ?? string.Empty) : string.Empty,
                                rawUrlParams.Count > 2 ? (rawUrlParams[2] ?? string.Empty) : string.Empty,
                            };

                            var requiredUrlIndices = GetRequiredDynamicUrlButtonIndices(templateRow?.UrlButtons);
                            var missingUrlIndex = requiredUrlIndices.FirstOrDefault(i => string.IsNullOrWhiteSpace(urlParams[i]));
                            if (requiredUrlIndices.Count > 0 && requiredUrlIndices.Any(i => string.IsNullOrWhiteSpace(urlParams[i])))
                            {
                                var msg =
                                    $"Missing dynamic URL button value for index {missingUrlIndex} on template '{templateName}'.";

                                _logger.LogWarning(
                                    "AutoReply template node {NodeId} in flow {FlowId} cannot send: {Message}",
                                    node.Id,
                                    flow.Id,
                                    msg);

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: msg,
                                    templateName: templateName,
                                    templateType: "AUTO_REPLY_TEMPLATE",
                                    cancellationToken: ct);

                                break;
                            }

                            var dto = new SimpleTemplateMessageDto
                            {
                                RecipientNumber = contactPhone,
                                TemplateName = templateName!,
                                TemplateParameters = bodyParams,
                                HasStaticButtons = false,
                                TemplateBody = cfg?.Body ?? templateRow?.Body ?? string.Empty,
                                LanguageCode = languageCode,
                                HeaderKind = headerKind,
                                HeaderMediaUrl = headerMediaUrl,
                                UrlButtonParams = urlParams
                            };

                            _logger.LogInformation(
                                "📄 AutoReplyRuntime sending template node {NodeId} (template={TemplateName}) for flow {FlowId}.",
                                node.Id,
                                templateName,
                                flow.Id);

                            // ✅ Now use DeliveryMode.Immediate (conversational template)
                            var sendResult = await _messageEngine.SendTemplateMessageSimpleAsync(
                                businessId,
                                dto,
                                DeliveryMode.Immediate);

                            if (!sendResult.Success)
                            {
                                _logger.LogWarning(
                                    "❌ AutoReplyRuntime failed to send template node {NodeId} in flow {FlowId}, Business={BusinessId}, Contact={ContactId}: {Message}",
                                    node.Id,
                                    flow.Id,
                                    businessId,
                                    contactId,
                                    sendResult.Message);

                                outcome.Notes = $"Failed to send template node {node.Id}: {sendResult.Message}";

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: sendResult.Message,
                                    templateName: templateName,
                                    templateType: "AUTO_REPLY_TEMPLATE",
                                    cancellationToken: ct);

                                return outcome;
                            }

                            outcome.AnySent = true;
                            outcome.SentTemplates++;
                            pieces.Add($"[TEMPLATE:{templateName}]");

                            await LogFlowStepAsync(
                                businessId,
                                flow,
                                node,
                                contactPhone,
                                messageLogId: null,
                                success: true,
                                errorMessage: null,
                                templateName: templateName,
                                templateType: "AUTO_REPLY_TEMPLATE",
                                cancellationToken: ct);

                            break;
                        }

                    case "tag":
                        {
                            var (success, appliedTags, error) =
                                await ApplyTagsToContactAsync(
                                    businessId,
                                    contactId,
                                    contactPhone,
                                    cfg,
                                    node.ConfigJson,
                                    ct);

                            if (!success)
                            {
                                _logger.LogWarning(
                                    "AutoReply tag node {NodeId} in flow {FlowId} failed to apply tags. Reason: {Reason}.",
                                    node.Id,
                                    flow.Id,
                                    error ?? "Unknown");

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: error ?? "Failed to apply tags",
                                    templateName: null,
                                    templateType: "AUTO_REPLY_TAG",
                                    cancellationToken: ct);

                                break;
                            }

                            _logger.LogInformation(
                                "🏷 AutoReply tag node {NodeId} in flow {FlowId} applied tags: {Tags}.",
                                node.Id,
                                flow.Id,
                                string.Join(",", appliedTags));

                            pieces.Add($"[TAGS:{string.Join(",", appliedTags)}]");

                            await LogFlowStepAsync(
                                businessId,
                                flow,
                                node,
                                contactPhone,
                                messageLogId: null,
                                success: true,
                                errorMessage: null,
                                templateName: null,
                                templateType: "AUTO_REPLY_TAG",
                                cancellationToken: ct);

                            break;
                        }

                    case "wait":
                        {
                            var seconds = cfg?.Seconds
                                           ?? cfg?.DelaySeconds
                                           ?? cfg?.WaitSeconds
                                           ?? 0;

                            if (seconds <= 0)
                            {
                                _logger.LogWarning(
                                    "AutoReply wait node {NodeId} in flow {FlowId} has invalid or zero Seconds.",
                                    node.Id,
                                    flow.Id);

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: "Invalid wait seconds",
                                    templateName: null,
                                    templateType: "AUTO_REPLY_WAIT",
                                    cancellationToken: ct);

                                break;
                            }

                            const int MaxWaitSeconds = 15;
                            var requestedSeconds = seconds;
                            if (seconds > MaxWaitSeconds)
                            {
                                seconds = MaxWaitSeconds;
                            }

                            _logger.LogInformation(
                                "⏱ AutoReply wait node {NodeId} in flow {FlowId} performing inline wait of {Seconds}s (requested {RequestedSeconds}s).",
                                node.Id,
                                flow.Id,
                                seconds,
                                requestedSeconds);

                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
                            }
                            catch (TaskCanceledException)
                            {
                                _logger.LogInformation(
                                    "AutoReply wait node {NodeId} in flow {FlowId} was cancelled during wait of {Seconds}s.",
                                    node.Id,
                                    flow.Id,
                                    seconds);
                                throw;
                            }

                            pieces.Add($"[WAIT:{seconds}s]");

                            await LogFlowStepAsync(
                                businessId,
                                flow,
                                node,
                                contactPhone,
                                messageLogId: null,
                                success: true,
                                errorMessage: null,
                                templateName: null,
                                templateType: "AUTO_REPLY_WAIT",
                                cancellationToken: ct);

                            break;
                        }

                    case "cta_flow":
                        {
                            // ⬇️ use the Guid parsed from the string
                            var ctaConfigId = cfg?.CtaFlowConfigGuid ?? Guid.Empty;
                            if (ctaConfigId == Guid.Empty)
                            {
                                _logger.LogWarning(
                                    "AutoReply CTA_FLOW node {NodeId} in flow {FlowId} has no CtaFlowConfigId configured.",
                                    node.Id,
                                    flow.Id);

                                await LogFlowStepAsync(
                                    businessId,
                                    flow,
                                    node,
                                    contactPhone,
                                    messageLogId: null,
                                    success: false,
                                    errorMessage: "Missing CtaFlowConfigId",
                                    templateName: null,
                                    templateType: "AUTO_REPLY_CTA_FLOW",
                                    cancellationToken: ct);

                                break;
                            }

                            _logger.LogInformation(
                                "🚀 AutoReply CTA_FLOW node {NodeId} in flow {FlowId} starting CTA flow config {ConfigId} for Business={BusinessId}, Contact={ContactId}.",
                                node.Id,
                                flow.Id,
                                ctaConfigId,
                                businessId,
                                contactId);

                            var runResult = await _ctaFlowRuntime.StartFlowAsync(
                                businessId,
                                contactId,
                                contactPhone,
                                ctaConfigId,
                                FlowExecutionOrigin.AutoReply,
                                flow.Id,
                                ct);

                            // ... existing success/error handling for CTA flow (unchanged) ...

                            break;
                        }

                    default:
                        {
                            _logger.LogDebug(
                                "AutoReplyRuntime encountered unsupported node type '{NodeType}' in flow {FlowId}, node {NodeId}.",
                                node.NodeType,
                                flow.Id,
                                node.Id);
                            break;
                        }

                }
            }

            if (pieces.Count > 0)
            {
                outcome.Notes = string.Join(" | ", pieces);
            }

            return outcome;
        }

        // ----------------------------------------------------
        // Helpers – keyword matching
        // ----------------------------------------------------
        private static List<string> SplitIntoWords(string normalizedIncoming)
        {
            if (string.IsNullOrWhiteSpace(normalizedIncoming))
                return new List<string>();

            var parts = normalizedIncoming.Split(
                new[]
                {
                    ' ', '\t', '\r', '\n',
                    '.', ',', '!', '?', ';', ':',
                    '-', '_', '/', '\\',
                    '(', ')', '[', ']', '{', '}',
                    '"', '\'', '’'
                },
                StringSplitOptions.RemoveEmptyEntries);

            return parts.ToList();
        }

        private static bool IsKeywordMatch(
            string normalizedKeyword,
            string normalizedIncoming,
            string matchMode,
            IReadOnlyList<string> incomingWords)
        {
            if (string.IsNullOrWhiteSpace(normalizedKeyword) ||
                string.IsNullOrWhiteSpace(normalizedIncoming))
            {
                return false;
            }

            var mode = string.IsNullOrWhiteSpace(matchMode)
                ? "WORD"
                : matchMode.Trim().ToUpperInvariant();

            switch (mode)
            {
                case "EXACT":
                    // Entire text must be exactly the keyword
                    return string.Equals(
                        normalizedIncoming,
                        normalizedKeyword,
                        StringComparison.Ordinal);

                case "WORD":
                    // For single words: token-based match (message must contain that word).
                    // For multi-word keywords: fall back to simple substring contains.
                    if (!normalizedKeyword.Contains(' '))
                    {
                        if (incomingWords == null || incomingWords.Count == 0)
                            return false;

                        foreach (var w in incomingWords)
                        {
                            if (string.Equals(w, normalizedKeyword, StringComparison.Ordinal))
                                return true;
                        }

                        return false;
                    }

                    return normalizedIncoming.Contains(
                        normalizedKeyword,
                        StringComparison.Ordinal);

                case "STARTSWITH":
                    return normalizedIncoming.StartsWith(
                        normalizedKeyword,
                        StringComparison.Ordinal);

                case "CONTAINS":
                default:
                    return normalizedIncoming.Contains(
                        normalizedKeyword,
                        StringComparison.Ordinal);
            }
        }

        private static bool IsValidHttpsUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return false;
            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<int> GetRequiredDynamicUrlButtonIndices(string? urlButtonsJson)
        {
            if (string.IsNullOrWhiteSpace(urlButtonsJson))
                return Array.Empty<int>();

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var btns = JsonSerializer.Deserialize<List<ButtonMetadataDto>>(urlButtonsJson, options)
                           ?? new List<ButtonMetadataDto>();

                return btns
                    .Where(b =>
                    {
                        var type = (b.Type ?? string.Empty).Trim().ToUpperInvariant();
                        var sub = (b.SubType ?? string.Empty).Trim().ToLowerInvariant();
                        var isUrl = type == "URL" || sub == "url";
                        var isDynamic = (b.ParameterValue ?? string.Empty).Contains("{{", StringComparison.Ordinal);
                        return isUrl && isDynamic;
                    })
                    .Select(b => b.Index)
                    .Where(i => i >= 0 && i <= 2)
                    .Distinct()
                    .OrderBy(i => i)
                    .ToList();
            }
            catch
            {
                return Array.Empty<int>();
            }
        }

        // ----------------------------------------------------
        // Helpers – nodes / configs
        // ----------------------------------------------------
        private FlowNodeSummary? GetFirstActionNodeSummary(AutoReplyFlow flow)
        {
            var nodes = DeserializeNodes(flow.NodesJson);
            if (nodes == null || nodes.Count == 0)
                return null;

            var firstAction = nodes
                .Where(n => !string.Equals(n.NodeType, "start", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Order)
                .FirstOrDefault();

            if (firstAction == null)
                return null;

            return new FlowNodeSummary(
                firstAction.NodeType ?? "?",
                firstAction.NodeName ?? "?");
        }

        /// <summary>
        /// Reads the first "message" node and extracts the text/body we want to send.
        /// Kept for compatibility; not used by the new multi-step runner.
        /// </summary>
        private string? GetSimpleReplyText(AutoReplyFlow flow)
        {
            var nodes = DeserializeNodes(flow.NodesJson);
            if (nodes == null || nodes.Count == 0)
                return null;

            var msgNode = nodes
                .Where(n =>
                    string.Equals(n.NodeType, "message", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Order)
                .FirstOrDefault();

            if (msgNode == null || string.IsNullOrWhiteSpace(msgNode.ConfigJson))
                return null;

            try
            {
                // ⚙️ IMPORTANT: make it case-insensitive so "text" / "body" also bind.
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var cfg = JsonSerializer.Deserialize<AutoReplyNodeConfig>(msgNode.ConfigJson, options);
                var text = cfg?.Text ?? cfg?.Body;

                return string.IsNullOrWhiteSpace(text)
                    ? null
                    : text.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to parse AutoReply message node config for flow {FlowId}. ConfigJson={ConfigJson}",
                    flow.Id,
                    msgNode.ConfigJson);

                return null;
            }
        }

        private static List<AutoReplyNodeRecord>? DeserializeNodes(string? nodesJson)
        {
            if (string.IsNullOrWhiteSpace(nodesJson))
                return null;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<List<AutoReplyNodeRecord>>(nodesJson, options);
            }
            catch
            {
                // If we cannot parse nodes, treat as no nodes.
                return null;
            }
        }

        // ----------------------------------------------------
        // Tag application helper (real DB write)
        // ----------------------------------------------------
        private async Task<(bool Success, string[] AppliedTags, string? ErrorMessage)> ApplyTagsToContactAsync(
            Guid businessId,
            Guid contactId,
            string contactPhone,
            AutoReplyNodeConfig? cfg,
            string? rawConfigJson,
            CancellationToken ct)
        {
            var rawNames = (cfg?.Tags ?? Array.Empty<string>()).ToList();
            var rawIds = (cfg?.TagIds ?? Array.Empty<string>()).ToList();

            // Back-compat: some older saved flows might have tags stored as a single string,
            // e.g. { "tags": "vip, hot" } instead of { "tags": ["vip","hot"] }.
            if (!string.IsNullOrWhiteSpace(rawConfigJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawConfigJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            var name = prop.Name ?? string.Empty;

                            if (name.Equals("tags", StringComparison.OrdinalIgnoreCase))
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                {
                                    var s = prop.Value.GetString() ?? string.Empty;
                                    rawNames.AddRange(
                                        s.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(x => x.Trim())
                                    );
                                }
                                else if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in prop.Value.EnumerateArray())
                                    {
                                        if (item.ValueKind == JsonValueKind.String)
                                            rawNames.Add(item.GetString() ?? string.Empty);
                                    }
                                }
                            }

                            if (name.Equals("tagIds", StringComparison.OrdinalIgnoreCase))
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                {
                                    var s = prop.Value.GetString() ?? string.Empty;
                                    rawIds.AddRange(
                                        s.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(x => x.Trim())
                                    );
                                }
                                else if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in prop.Value.EnumerateArray())
                                    {
                                        if (item.ValueKind == JsonValueKind.String)
                                            rawIds.Add(item.GetString() ?? string.Empty);
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore parse errors; configs can be arbitrary
                }
            }

            var tagNames = rawNames
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tagIds = rawIds
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Select(t => Guid.TryParse(t, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList();

            if (tagNames.Count == 0 && tagIds.Count == 0)
            {
                return (false, Array.Empty<string>(), "No tags configured");
            }

            Contact? contact = null;

            if (contactId != Guid.Empty)
            {
                contact = await _dbContext.Contacts
                    .Include(c => c.ContactTags)
                    .FirstOrDefaultAsync(c =>
                        c.Id == contactId &&
                        c.BusinessId == businessId &&
                        c.IsActive &&
                        !c.IsArchived,
                        ct);
            }

            // Fallback: if contact id wasn't provided or lookup failed, try phone.
            if (contact == null && !string.IsNullOrWhiteSpace(contactPhone))
            {
                var phone = contactPhone.Trim();
                contact = await _dbContext.Contacts
                    .Include(c => c.ContactTags)
                    .FirstOrDefaultAsync(c =>
                        c.BusinessId == businessId &&
                        c.IsActive &&
                        !c.IsArchived &&
                        c.PhoneNumber == phone,
                        ct);
            }

            if (contact == null)
            {
                return (false, Array.Empty<string>(), "Contact not found");
            }

            var now = DateTime.UtcNow;
            var existingTagIds = contact.ContactTags?.Select(ct => ct.TagId).ToHashSet() ?? new HashSet<Guid>();

            var tagsToLink = new List<Tag>();

            // 1) Resolve explicit TagIds (if builder ever stores ids)
            if (tagIds.Count > 0)
            {
                var byId = await _dbContext.Tags
                    .Where(t => t.BusinessId == businessId && t.IsActive && tagIds.Contains(t.Id))
                    .ToListAsync(ct);
                tagsToLink.AddRange(byId);
            }

            // 2) Resolve Tag names (create missing tags when needed)
            if (tagNames.Count > 0)
            {
                var nameLowers = tagNames.Select(n => n.ToLowerInvariant()).ToList();

                var existing = await _dbContext.Tags
                    .Where(t => t.BusinessId == businessId && t.IsActive && nameLowers.Contains((t.Name ?? "").ToLower()))
                    .ToListAsync(ct);

                var existingByLower = existing
                    .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                    .GroupBy(t => t.Name.Trim().ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var name in tagNames)
                {
                    var key = name.ToLowerInvariant();
                    if (existingByLower.TryGetValue(key, out var found))
                    {
                        tagsToLink.Add(found);
                        continue;
                    }

                    var created = new Tag
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Name = name,
                        ColorHex = "#8c8c8c",
                        Category = "General",
                        IsActive = true,
                        CreatedAt = now,
                        LastUsedAt = now
                    };

                    _dbContext.Tags.Add(created);
                    tagsToLink.Add(created);
                    existingByLower[key] = created;
                }

                await _dbContext.SaveChangesAsync(ct); // persist new tags before linking
            }

            tagsToLink = tagsToLink
                .Where(t => t != null && t.BusinessId == businessId && t.IsActive)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            if (tagsToLink.Count == 0)
            {
                return (false, Array.Empty<string>(), "No valid tags found for business");
            }

            contact.ContactTags ??= new List<ContactTag>();

            foreach (var tag in tagsToLink)
            {
                tag.LastUsedAt = now;

                if (existingTagIds.Contains(tag.Id))
                    continue;

                contact.ContactTags.Add(new ContactTag
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    ContactId = contact.Id,
                    TagId = tag.Id,
                    AssignedAt = now,
                    AssignedBy = "automation"
                });

                existingTagIds.Add(tag.Id);
            }

            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AutoReply tag assignment failed. businessId={BusinessId}, contactId={ContactId}",
                    businessId,
                    contact.Id);

                return (false, Array.Empty<string>(), "Failed to apply tags (DB error)");
            }

            var applied = tagsToLink
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return (true, applied, null);
        }

        // ----------------------------------------------------
        // AutoReply logging helper
        // ----------------------------------------------------
        private async Task LogAutoReplyAsync(
            Guid businessId,
            Guid contactId,
            string? matchedKeyword,
            AutoReplyFlow? flow,
            string? replyText,
            Guid? messageLogId,
            CancellationToken ct)
        {
            try
            {
                var log = new AutoReplyLog
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    ContactId = contactId,
                    TriggerType = "flow",
                    TriggerKeyword = matchedKeyword ?? string.Empty,
                    ReplyContent = replyText ?? string.Empty,
                    FlowName = flow?.Name,
                    MessageLogId = messageLogId,
                    TriggeredAt = DateTime.UtcNow
                };

                _dbContext.Set<AutoReplyLog>().Add(log);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "📝 AutoReplyLog inserted for BusinessId={BusinessId}, ContactId={ContactId}, FlowName={FlowName}, Keyword='{Keyword}'.",
                    businessId,
                    contactId,
                    flow?.Name,
                    matchedKeyword);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "❌ Failed to insert AutoReplyLog for BusinessId={BusinessId}, ContactId={ContactId}, FlowName={FlowName}.",
                    businessId,
                    contactId,
                    flow?.Name);
            }
        }

        /// <summary>
        /// Helper to write a single origin-aware FlowExecutionLog row for an AutoReply step.
        /// </summary>
        private async Task LogFlowStepAsync(
           Guid businessId,
           AutoReplyFlow flow,
           AutoReplyNodeRecord node,
           string contactPhone,
           Guid? messageLogId,
           bool success,
           string? errorMessage,
           string? templateName,
           string? templateType,
           CancellationToken cancellationToken)
        {
            try
            {
                if (flow == null)
                {
                    _logger.LogWarning(
                        "Skipping FlowExecution log because AutoReplyFlow is null. BusinessId={BusinessId}, Contact={ContactPhone}",
                        businessId,
                        contactPhone);

                    return;
                }

                if (node == null)
                {
                    _logger.LogWarning(
                        "Skipping FlowExecution log because AutoReply node is null. BusinessId={BusinessId}, AutoReplyFlowId={FlowId}, Contact={ContactPhone}",
                        businessId,
                        flow.Id,
                        contactPhone);

                    return;
                }

                // AutoReplyNodeRecord.Id is Guid?; if somehow missing, keep Guid.Empty so we can see it in logs.
                var stepId = node.Id ?? Guid.Empty;

                var ctx = new FlowExecutionContext
                {
                    BusinessId = businessId,
                    Origin = FlowExecutionOrigin.AutoReply,

                    // For AutoReply we DO NOT use CTA FlowId; that column is reserved for CTAFlowConfigId.
                    FlowId = null,

                    StepId = stepId,
                    StepName = node.NodeName ?? node.NodeType ?? "AUTO_REPLY_STEP",

                    // No per-run grouping yet; can add when you introduce FlowRunId
                    RunId = null,

                    // AutoReply-origin, not a campaign broadcast
                    CampaignId = null,
                    AutoReplyFlowId = flow.Id,
                    CampaignSendLogId = null,
                    TrackingLogId = null,

                    MessageLogId = messageLogId,
                    ContactPhone = contactPhone,

                    // No buttons involved for plain AutoReply nodes (we log button stuff in CTAFlowRuntime)
                    TriggeredByButton = null,
                    ButtonIndex = null,

                    TemplateName = templateName,
                    TemplateType = templateType,

                    RequestId = null,
                    Success = success,
                    ErrorMessage = errorMessage,
                    RawResponse = null,

                    ExecutedAtUtc = DateTime.UtcNow
                };

                await _flowExecutionLogger.LogStepAsync(ctx, cancellationToken);

                _logger.LogInformation(
                    "[AutoReplyFlowLog] Logged step. Biz={BusinessId}, AutoReplyFlowId={FlowId}, StepId={StepId}, StepName={StepName}, Success={Success}, Template={TemplateName}",
                    businessId,
                    flow.Id,
                    stepId,
                    ctx.StepName,
                    ctx.Success,
                    ctx.TemplateName ?? "(none)");
            }
            catch (Exception ex)
            {
                // Never break AutoReply runtime because logging failed.
                _logger.LogError(
                    ex,
                    "Failed to log AutoReply FlowExecution step for BusinessId={BusinessId}, AutoReplyFlowId={FlowId}, NodeId={NodeId}",
                    businessId,
                    flow?.Id,
                    node?.Id);
            }
        }

        // ----------------------------------------------------
        // Private types for node parsing / choosing winners
        // ----------------------------------------------------

        private sealed class AutoReplyNodeRecord
        {
            public Guid? Id { get; set; }
            public string? NodeType { get; set; }
            public string? NodeName { get; set; }
            public string? ConfigJson { get; set; }
            public int Order { get; set; }
            // positionX/positionY etc exist in JSON but we don't need them here
        }

        private sealed class AutoReplyNodeConfig
        {
            public string? Text { get; set; }           // for "message" nodes
            public string? Body { get; set; }           // sometimes templates/body may reuse this
            public string? TemplateName { get; set; }   // for template nodes

            // Template node – dynamic values (CTA-like)
            public List<string>? BodyParams { get; set; }        // {{1}}, {{2}}, ...
            public List<string>? Placeholders { get; set; }      // legacy alias
            public string? HeaderMediaUrl { get; set; }          // image/video/document URL
            public List<string>? UrlButtonParams { get; set; }   // index 0..2 URL params
            public bool? UseProfileName { get; set; }            // auto-fill one slot
            public int? ProfileNameSlot { get; set; }            // 1-based slot index

            // Tag node – support both "tags" and "tagIds" shapes
            public string[]? Tags { get; set; }
            public string[]? TagIds { get; set; }

            // Wait node – support multiple property names coming from builder
            public int? Seconds { get; set; }
            public int? DelaySeconds { get; set; }
            public int? WaitSeconds { get; set; }

            // CTA flow node – ID of the CTA flow config to start
            public string? CtaFlowConfigId { get; set; }

            [JsonIgnore]
            public Guid? CtaFlowConfigGuid =>
              Guid.TryParse(CtaFlowConfigId, out var g) ? g : (Guid?)null;
        }

        private sealed record FlowNodeSummary(string NodeType, string NodeName);

        private sealed class FlowMatchCandidate
        {
            public AutoReplyFlow Flow { get; init; } = null!;
            public string Keyword { get; init; } = string.Empty;
            public string MatchMode { get; init; } = "WORD";
            public int Priority { get; init; }
            public int KeywordLength { get; init; }
        }
    }
}







