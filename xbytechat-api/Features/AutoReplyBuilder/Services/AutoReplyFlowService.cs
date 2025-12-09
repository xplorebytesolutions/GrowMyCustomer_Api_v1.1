using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using xbytechat.api;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;
using xbytechat.api.Features.AutoReplyBuilder.Models;

namespace xbytechat.api.Features.AutoReplyBuilder.Services
{
    /// <summary>
    /// Minimal CRUD service for the Auto-Reply flow builder.
    /// Keeps one flow -> many nodes persisted in PostgreSQL.
    /// </summary>
    public class AutoReplyFlowService : IAutoReplyFlowService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AutoReplyFlowService> _log;

        public AutoReplyFlowService(AppDbContext db, ILogger<AutoReplyFlowService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<IReadOnlyList<AutoReplyFlowSummaryDto>> GetFlowsForBusinessAsync(Guid businessId, CancellationToken ct = default)
        {
            return await _db.AutoReplyFlows
                .AsNoTracking()
                .Where(f => f.BusinessId == businessId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new AutoReplyFlowSummaryDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    IsActive = f.IsActive,
                    TriggerKeyword = f.TriggerKeyword,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt // entity does not currently track updates
                })
                .ToListAsync(ct);
        }

        public async Task<AutoReplyFlowDto?> GetFlowAsync(Guid businessId, Guid flowId, CancellationToken ct = default)
        {
            var flow = await _db.AutoReplyFlows
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId, ct);

            if (flow == null)
            {
                return null;
            }

            var nodes = await _db.AutoReplyNodes
                .AsNoTracking()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.Order)
                .ThenBy(n => n.CreatedAt)
                .ToListAsync(ct);

            return MapToDto(flow, nodes);
        }

        public async Task<AutoReplyFlowDto> SaveFlowAsync(
        Guid businessId,
        AutoReplyFlowDto dto,
        CancellationToken ct = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Name is required", nameof(dto));

            _log.LogInformation(
                "[AutoReplyFlow] SaveFlowAsync biz={BusinessId} flow={FlowName}",
                businessId,
                dto.Name);

            var flow = dto.Id.HasValue
                ? await _db.AutoReplyFlows.FirstOrDefaultAsync(
                    f => f.Id == dto.Id.Value && f.BusinessId == businessId,
                    ct)
                : null;

            var isNew = flow == null;
            if (isNew)
            {
                flow = new AutoReplyFlow
                {
                    Id = dto.Id ?? Guid.NewGuid(),
                    BusinessId = businessId,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.AutoReplyFlows.Add(flow);
            }

            // 🔧 Core fields
            flow.Name = dto.Name.Trim();
            flow.TriggerKeyword = dto.TriggerKeyword?.Trim();
            flow.Keyword = dto.TriggerKeyword?.Trim(); // legacy alias used elsewhere
            flow.IsActive = dto.IsActive;
            flow.IndustryTag = dto.IndustryTag;
            flow.UseCase = dto.UseCase;
            flow.IsDefaultTemplate = dto.IsDefaultTemplate;

            // 🕒 Always stamp "last updated"
            flow.UpdatedAt = DateTime.UtcNow;

            // Ensure required JSON columns never stay null (legacy storage)
            flow.NodesJson = string.IsNullOrWhiteSpace(flow.NodesJson) ? "[]" : flow.NodesJson;
            flow.EdgesJson = string.IsNullOrWhiteSpace(flow.EdgesJson) ? "[]" : flow.EdgesJson;

            // Replace nodes wholesale for now (simple + safe for MVP)
            var existingNodes = await _db.AutoReplyNodes
                .Where(n => n.FlowId == flow.Id)
                .ToListAsync(ct);

            if (existingNodes.Count > 0)
            {
                _db.AutoReplyNodes.RemoveRange(existingNodes);
            }

            var incomingNodes = dto.Nodes ?? new List<AutoReplyNodeDto>();

            var newNodes = incomingNodes.Select((n, index) => new AutoReplyFlowNode
            {
                Id = n.Id == null || n.Id == Guid.Empty ? Guid.NewGuid() : n.Id.Value,
                FlowId = flow.Id,
                NodeType = n.NodeType,
                Label = n.Label ?? string.Empty,
                NodeName = string.IsNullOrWhiteSpace(n.NodeName) ? n.Label : n.NodeName,
                ConfigJson = string.IsNullOrWhiteSpace(n.ConfigJson) ? "{}" : n.ConfigJson,
                Position = new Position
                {
                    X = n.PositionX,
                    Y = n.PositionY
                },
                Order = n.Order != 0 ? n.Order : index,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            if (newNodes.Count > 0)
            {
                _db.AutoReplyNodes.AddRange(newNodes);

                // 🔁 Keep NodesJson in sync with the rows (only fields runtime cares about)
                var nodesJsonPayload = newNodes
                    .Select(n => new
                    {
                        Id = (Guid?)n.Id,
                        NodeType = n.NodeType,
                        NodeName = n.NodeName,
                        ConfigJson = n.ConfigJson,
                        Order = n.Order
                    })
                    .ToList();

                flow.NodesJson = System.Text.Json.JsonSerializer.Serialize(nodesJsonPayload);
            }
            else
            {
                flow.NodesJson = "[]";
            }

            await _db.SaveChangesAsync(ct);

            return MapToDto(flow, newNodes);
        }



        public async Task DeleteFlowAsync(Guid businessId, Guid flowId, CancellationToken ct = default)
        {
            var flow = await _db.AutoReplyFlows.FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId, ct);
            if (flow == null)
            {
                _log.LogInformation("[AutoReplyFlow] Delete skipped - not found biz={BusinessId} flowId={FlowId}", businessId, flowId);
                return;
            }

            _db.AutoReplyFlows.Remove(flow); // nodes cascade via FK
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("[AutoReplyFlow] Deleted biz={BusinessId} flowId={FlowId}", businessId, flowId);
        }
        public async Task SetActiveAsync(
    Guid businessId,
    Guid flowId,
    bool isActive,
    CancellationToken ct = default)
        {
            var flow = await _db.AutoReplyFlows
                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId, ct);

            if (flow == null)
            {
                _log.LogWarning(
                    "[AutoReplyFlow] SetActiveAsync: flow not found. Biz={BusinessId}, Flow={FlowId}",
                    businessId,
                    flowId);
                return;
            }

            if (flow.IsActive == isActive)
            {
                _log.LogDebug(
                    "[AutoReplyFlow] SetActiveAsync: no-op, already IsActive={IsActive}. Biz={BusinessId}, Flow={FlowId}",
                    isActive,
                    businessId,
                    flowId);
                return;
            }

            flow.IsActive = isActive;
            flow.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "[AutoReplyFlow] SetActiveAsync: updated IsActive={IsActive}. Biz={BusinessId}, Flow={FlowId}",
                isActive,
                businessId,
                flowId);
        }

        private static AutoReplyFlowDto MapToDto(
     AutoReplyFlow flow,
     IReadOnlyCollection<AutoReplyFlowNode> nodes)
        {
            return new AutoReplyFlowDto
            {
                Id = flow.Id,
                Name = flow.Name,
                IsActive = flow.IsActive,
                TriggerKeyword = flow.TriggerKeyword ?? flow.Keyword,
                IndustryTag = flow.IndustryTag,
                UseCase = flow.UseCase,
                IsDefaultTemplate = flow.IsDefaultTemplate,
                CreatedAt = flow.CreatedAt,
                UpdatedAt = flow.UpdatedAt,
                Nodes = nodes.Select(n => new AutoReplyNodeDto
                {
                    Id = n.Id,
                    NodeType = n.NodeType,
                    Label = n.Label,
                    NodeName = n.NodeName,
                    ConfigJson = n.ConfigJson,
                    PositionX = n.Position?.X ?? 0,
                    PositionY = n.Position?.Y ?? 0,
                    Order = n.Order
                }).ToList()
            };
        }

    }
}
