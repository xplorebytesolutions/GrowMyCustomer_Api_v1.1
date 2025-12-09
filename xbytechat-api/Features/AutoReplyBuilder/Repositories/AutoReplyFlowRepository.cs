using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Repositories;
using xbytechat.api.Features.AutoReplyBuilder.Models;

public class AutoReplyFlowRepository : IAutoReplyFlowRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<AutoReplyFlowRepository> _logger;

    public AutoReplyFlowRepository(AppDbContext context, ILogger<AutoReplyFlowRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AutoReplyFlow> SaveAsync(AutoReplyFlow flow)
    {
        _context.AutoReplyFlows.Add(flow);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "❌ Save failed: {0}", ex.InnerException?.Message);
            throw;
        }

        return flow;
    }

    public async Task SaveNodesAndEdgesAsync(IEnumerable<AutoReplyFlowNode> nodes, IEnumerable<AutoReplyFlowEdge> edges)
    {
        _context.AutoReplyFlowNodes.AddRange(nodes);
        _context.AutoReplyFlowEdges.AddRange(edges);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "❌ Save failed: {0}", ex.InnerException?.Message);
            throw;
        }

    }

    public async Task<List<AutoReplyFlow>> GetAllByBusinessIdAsync(Guid businessId)
    {
        return await _context.AutoReplyFlows
            .Where(f => f.BusinessId == businessId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<AutoReplyFlow?> GetByIdAsync(Guid flowId, Guid businessId)
    {
        return await _context.AutoReplyFlows
            .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId);
    }

    public async Task<int> GetFlowCountAsync(Guid businessId)
    {
        return await _context.AutoReplyFlows.CountAsync(f => f.BusinessId == businessId);
    }

    public async Task<bool> RenameFlowAsync(Guid id, string newName)
    {
        var flow = await _context.AutoReplyFlows.FindAsync(id);
        if (flow == null) return false;

        flow.Name = newName;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "❌ Save failed: {0}", ex.InnerException?.Message);
            throw;
        }

        return true;
    }

    public async Task<bool> DeleteFlowAsync(Guid id, Guid businessId)
    {
        var flow = await _context.AutoReplyFlows
            .FirstOrDefaultAsync(f => f.Id == id && f.BusinessId == businessId);

        if (flow == null) return false;

        _context.AutoReplyFlows.Remove(flow);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "❌ Save failed: {0}", ex.InnerException?.Message);
            throw;
        }

        return true;
    }
    public async Task<List<AutoReplyFlowNode>> GetNodesByFlowIdAsync(Guid flowId)
    {
        return await _context.AutoReplyFlowNodes
            .Where(n => n.FlowId == flowId)
            .ToListAsync();
    }

    public async Task<List<AutoReplyFlowEdge>> GetEdgesByFlowIdAsync(Guid flowId)
    {
        return await _context.AutoReplyFlowEdges
            .Where(e => e.FlowId == flowId)
            .ToListAsync();
    }
    public async Task<AutoReplyFlow?> FindFlowByKeywordAsync(Guid businessId, string keyword)
    {
        return await _context.AutoReplyFlows
            .Where(f => f.BusinessId == businessId && f.IsActive && f.TriggerKeyword == keyword)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync();
    }
    public async Task<List<AutoReplyFlowNode>> GetStructuredNodesAsync(Guid flowId)
    {
        return await _context.AutoReplyFlowNodes
            .Where(n => n.FlowId == flowId)
            .ToListAsync();
    }

    public async Task<List<AutoReplyFlowEdge>> GetStructuredEdgesAsync(Guid flowId)
    {
        return await _context.AutoReplyFlowEdges
            .Where(e => e.FlowId == flowId)
            .ToListAsync();
    }

}
