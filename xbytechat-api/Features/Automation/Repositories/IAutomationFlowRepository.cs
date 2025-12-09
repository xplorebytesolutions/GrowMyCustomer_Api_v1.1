using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.Automation.Models;

namespace xbytechat.api.Features.Automation.Repositories
{
    public interface IAutomationFlowRepository
    {
        // 🔍 Get flow by unique FlowId + BusinessId (strict filtering)
        Task<AutomationFlow?> GetByIdAsync(Guid flowId, Guid businessId);

        // 🔍 Get flow by keyword for auto-trigger
        Task<AutomationFlow?> GetFlowByKeywordAsync(Guid businessId, string keyword);

        // 📋 List all flows for business
        Task<IEnumerable<AutomationFlow>> GetAllByBusinessAsync(Guid businessId);

        // ➕ Create flow
        Task<AutomationFlow> CreateAsync(AutomationFlow flow);

        // ✏️ Update flow
        Task<AutomationFlow> UpdateAsync(AutomationFlow flow);

        // ❌ Delete flow
        Task<bool> DeleteAsync(Guid flowId, Guid businessId);
        Task<AutomationFlow?> GetByKeywordAsync(Guid businessId, string keyword);


    }
}

