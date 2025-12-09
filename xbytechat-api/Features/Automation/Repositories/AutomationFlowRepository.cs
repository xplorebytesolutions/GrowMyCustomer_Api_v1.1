using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Automation.Models;

namespace xbytechat.api.Features.Automation.Repositories
{
    public class AutomationFlowRepository : IAutomationFlowRepository
    {
        private readonly AppDbContext _db;

        public AutomationFlowRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<AutomationFlow?> GetByIdAsync(Guid flowId, Guid businessId)
        {
            return await _db.AutomationFlows
                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId && f.IsActive);
        }

        public async Task<AutomationFlow?> GetFlowByKeywordAsync(Guid businessId, string keyword)
        {
            return await _db.AutomationFlows
                .FirstOrDefaultAsync(f =>
                    f.BusinessId == businessId &&
                    f.TriggerKeyword.ToLower() == keyword.ToLower() &&
                    f.IsActive);
        }

        public async Task<IEnumerable<AutomationFlow>> GetAllByBusinessAsync(Guid businessId)
        {
            return await _db.AutomationFlows
                .Where(f => f.BusinessId == businessId && f.IsActive)
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<AutomationFlow> CreateAsync(AutomationFlow flow)
        {
            flow.Id = Guid.NewGuid();
            flow.CreatedAt = DateTime.UtcNow;
            flow.IsActive = true;

            _db.AutomationFlows.Add(flow);
            await _db.SaveChangesAsync();
            return flow;
        }

        public async Task<AutomationFlow> UpdateAsync(AutomationFlow flow)
        {
            var existing = await _db.AutomationFlows
                .FirstOrDefaultAsync(f => f.Id == flow.Id && f.BusinessId == flow.BusinessId && f.IsActive);

            if (existing == null)
                throw new KeyNotFoundException("Automation flow not found.");

            existing.Name = flow.Name;
            existing.TriggerKeyword = flow.TriggerKeyword;
            existing.NodesJson = flow.NodesJson;
            existing.EdgesJson = flow.EdgesJson;
            existing.UpdatedAt = DateTime.UtcNow;

            _db.AutomationFlows.Update(existing);
            await _db.SaveChangesAsync();

            return existing;
        }

        public async Task<bool> DeleteAsync(Guid flowId, Guid businessId)
        {
            var flow = await _db.AutomationFlows
                .FirstOrDefaultAsync(f => f.Id == flowId && f.BusinessId == businessId && f.IsActive);

            if (flow == null)
                return false;

            flow.IsActive = false;
            flow.UpdatedAt = DateTime.UtcNow;

            _db.AutomationFlows.Update(flow);
            await _db.SaveChangesAsync();
            return true;
        }
        public async Task<AutomationFlow?> GetByKeywordAsync(Guid businessId, string keyword)
        {
            return await _db.AutomationFlows
                .FirstOrDefaultAsync(f =>
                f.BusinessId == businessId &&
                EF.Functions.ILike(f.TriggerKeyword, keyword) &&
                f.IsActive);
        }


    }
}
