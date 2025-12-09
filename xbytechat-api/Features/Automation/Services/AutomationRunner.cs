using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using xbytechat.api.Features.Automation.Models;
using xbytechat.api.Features.Automation.Models.Configs;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.Inbox.Models;
using xbytechat.api.Features.Automation.Config;
using xbytechat.api.Features.CRM.Interfaces;

namespace xbytechat.api.Features.Automation.Services
{
    public class AutomationRunner : IAutomationRunner
    {
        private readonly IMessageEngineService _messageService;
        private readonly IContactService _contactService;
        private readonly ILogger<AutomationRunner> _logger;
        private readonly AppDbContext _appDbContext;
        public AutomationRunner(
            IMessageEngineService messageService,
            IContactService contactService,
            ILogger<AutomationRunner> logger, AppDbContext appDbContext)
        {
            _messageService = messageService;
            _contactService = contactService;
            _logger = logger;
            _appDbContext = appDbContext;
        }

        public async Task<AutomationFlowRunResult> RunFlowAsync(
            AutomationFlow flow,
            Guid businessId,
            Guid contactId,
            string contactPhone,
            string sourceChannel,
            string industryTag)
        {
            var nodes = JsonConvert.DeserializeObject<List<AutomationFlowNode>>(flow.NodesJson);
            var edges = JsonConvert.DeserializeObject<List<AutomationFlowEdge>>(flow.EdgesJson);

            var result = new AutomationFlowRunResult();
            var currentNode = nodes.FirstOrDefault(); // Start from first node

            if (currentNode == null)
            {
                _logger.LogWarning("🚫 Flow has no start node.");
                result.NeedsAgent = true;
                result.Notes = "No start node found.";
                return result;
            }

            while (currentNode != null)
            {
                _logger.LogInformation("➡️ Running node: {NodeType} | {NodeId}", currentNode.NodeType, currentNode.Id);

                switch (currentNode.NodeType)
                {
                    case NodeTypeEnum.Message:
                        var msgCfg = JsonConvert.DeserializeObject<MessageConfig>(currentNode.ConfigJson);
                        var msgDto = new TextMessageSendDto
                        {
                            BusinessId = businessId,
                            ContactId = contactId,
                            RecipientNumber = contactPhone,
                            TextContent = msgCfg.Text,
                            Source = "automation"
                        };
                        _logger.LogInformation("📤 Sending message: {Text}", msgCfg.Text);
                        await _messageService.SendAutomationReply(msgDto);
                        break;

                    case NodeTypeEnum.Wait:
                        var waitCfg = JsonConvert.DeserializeObject<WaitConfig>(currentNode.ConfigJson);
                        _logger.LogInformation("⏳ Waiting {Seconds}s", waitCfg.Seconds);
                        await Task.Delay(waitCfg.Seconds * 1000);
                        break;

                    case NodeTypeEnum.Tag:
                        var tagCfg = JsonConvert.DeserializeObject<TagNodeConfig>(currentNode.ConfigJson);
                        _logger.LogInformation("🏷️ Assigning tags: {Tags}", string.Join(", ", tagCfg.Tags));
                        await _contactService.AssignTagsAsync(businessId, contactPhone, tagCfg.Tags);
                        break;

                    case NodeTypeEnum.AgentHandoff:
                        result.NeedsAgent = true;
                        if (Guid.TryParse(currentNode.Id, out var parsedId))
                        {
                            result.HandoffNodeId = parsedId;
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Invalid node ID format for AgentHandoff node: {Id}", currentNode.Id);
                            result.HandoffNodeId = null;
                        }
                        result.Notes = "Flow routed to human agent.";
                        return result;

                    case NodeTypeEnum.End:
                        _logger.LogInformation("✅ End node reached.");
                        currentNode = null;
                        continue;

                    case NodeTypeEnum.Choice:
                        _logger.LogInformation("🧠 Reached Choice node. Saving session state to wait for user input...");

                        var session = await _appDbContext.ChatSessionStates.FirstOrDefaultAsync(s =>
                            s.BusinessId == businessId && s.ContactId == contactId);

                        if (session == null)
                        {
                            session = new ChatSessionState
                            {
                                Id = Guid.NewGuid(),
                                BusinessId = businessId,
                                ContactId = contactId
                            };
                            _appDbContext.ChatSessionStates.Add(session);
                        }

                        session.Mode = "awaiting_choice";
                        session.UpdatedBy = currentNode.Id.ToString();
                        session.LastUpdatedAt = DateTime.UtcNow;

                        await _appDbContext.SaveChangesAsync();

                        result.Notes = "Choice node reached. Flow paused.";
                        return result;
                }

                var edge = edges.FirstOrDefault(e => e.SourceNodeId == currentNode.Id);
                currentNode = edge == null ? null : nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
            }

            result.Notes = "Flow completed.";
            return result;
        }
        public async Task<AutomationFlowRunResult> ResumeFlowAsync(
           Guid businessId,
           Guid contactId,
           string contactPhone,
           string incomingMessage)
        {
            var session = await _appDbContext.ChatSessionStates
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.ContactId == contactId);

            if (session == null || session.Mode != "awaiting_choice")
            {
                _logger.LogWarning("❌ No active automation session found or mode not awaiting_choice.");
                return new AutomationFlowRunResult { NeedsAgent = true, Notes = "No active automation session." };
            }

            var flow = await _appDbContext.AutomationFlows
                .Where(f => f.BusinessId == businessId && f.IsActive)
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();

            if (flow == null)
            {
                _logger.LogWarning("❌ No active automation flow found for business.");
                return new AutomationFlowRunResult { NeedsAgent = true, Notes = "No active flow found." };
            }

            var nodes = JsonConvert.DeserializeObject<List<AutomationFlowNode>>(flow.NodesJson);
            var edges = JsonConvert.DeserializeObject<List<AutomationFlowEdge>>(flow.EdgesJson);

            var choiceNode = nodes.FirstOrDefault(n => n.Id == session.UpdatedBy && n.NodeType == NodeTypeEnum.Choice);
            if (choiceNode == null)
            {
                _logger.LogWarning("❌ Stored session node not found or not a Choice node.");
                return new AutomationFlowRunResult { NeedsAgent = true, Notes = "Invalid Choice node in session." };
            }

            var cfg = JsonConvert.DeserializeObject<ChoiceConfig>(choiceNode.ConfigJson);
            if (cfg?.Conditions == null)
            {
                _logger.LogWarning("❌ Choice config is null or empty.");
                return new AutomationFlowRunResult { NeedsAgent = true, Notes = "Invalid Choice config." };
            }

            var match = cfg.Conditions.FirstOrDefault(c =>
                string.Equals(c.Match.Trim(), incomingMessage.Trim(), StringComparison.OrdinalIgnoreCase));

            string nextNodeId = match?.NextNodeId ?? cfg.FallbackNodeId;
            if (match == null)
            {
                _logger.LogWarning("🔁 No matching condition found. Using fallback: {Fallback}", nextNodeId);
            }

            var nextNode = nodes.FirstOrDefault(n => n.Id == nextNodeId);
            if (nextNode == null)
            {
                _logger.LogWarning("❌ Next node after choice not found.");
                return new AutomationFlowRunResult { NeedsAgent = true, Notes = "Next node not found." };
            }

            // ✅ Clear session after resume
            _appDbContext.ChatSessionStates.Remove(session);
            await _appDbContext.SaveChangesAsync();

            // ✅ Resume from the matched node using shared loop
            return await ExecuteNodeLoopAsync(flow, nextNode, nodes, edges, businessId, contactId, contactPhone);
        }


        private async Task<AutomationFlowRunResult> ExecuteNodeLoopAsync(
    AutomationFlow flow,
    AutomationFlowNode startNode,
    List<AutomationFlowNode> nodes,
    List<AutomationFlowEdge> edges,
    Guid businessId,
    Guid contactId,
    string contactPhone)
        {
            var result = new AutomationFlowRunResult();
            var currentNode = startNode;

            while (currentNode != null)
            {
                _logger.LogInformation("➡️ Executing node: {NodeType} | {NodeId}", currentNode.NodeType, currentNode.Id);

                switch (currentNode.NodeType)
                {
                    case NodeTypeEnum.Message:
                        var msgCfg = JsonConvert.DeserializeObject<MessageConfig>(currentNode.ConfigJson);
                        var msgDto = new TextMessageSendDto
                        {
                            BusinessId = businessId,
                            ContactId = contactId,
                            RecipientNumber = contactPhone,
                            TextContent = msgCfg.Text,
                            Source = "automation"
                        };
                        await _messageService.SendAutomationReply(msgDto);
                        break;

                    case NodeTypeEnum.Tag:
                        var tagCfg = JsonConvert.DeserializeObject<TagNodeConfig>(currentNode.ConfigJson);
                        await _contactService.AssignTagsAsync(businessId, contactPhone, tagCfg.Tags);
                        break;

                    case NodeTypeEnum.Wait:
                        var waitCfg = JsonConvert.DeserializeObject<WaitConfig>(currentNode.ConfigJson);
                        await Task.Delay(waitCfg.Seconds * 1000);
                        break;

                    case NodeTypeEnum.End:
                        return new AutomationFlowRunResult { Notes = "✅ Flow ended." };

                    case NodeTypeEnum.AgentHandoff:
                        return new AutomationFlowRunResult
                        {
                            NeedsAgent = true,
                            Notes = "Routed to human agent."
                        };

                    case NodeTypeEnum.Choice:
                        var session = new ChatSessionState
                        {
                            Id = Guid.NewGuid(),
                            BusinessId = businessId,
                            ContactId = contactId,
                            Mode = "awaiting_choice",
                            UpdatedBy = currentNode.Id.ToString(),
                            LastUpdatedAt = DateTime.UtcNow
                        };
                        _appDbContext.ChatSessionStates.Add(session);
                        await _appDbContext.SaveChangesAsync();

                        return new AutomationFlowRunResult { Notes = "Paused at Choice node." };
                }

                var edge = edges.FirstOrDefault(e => e.SourceNodeId == currentNode.Id);
                currentNode = edge == null ? null : nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
            }

            return new AutomationFlowRunResult { Notes = "Flow completed." };
        }

    }
}
