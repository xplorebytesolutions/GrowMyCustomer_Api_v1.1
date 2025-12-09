using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.Automation.Models;

namespace xbytechat.api.Features.Automation.Services
{
    public interface IAutomationService
    {
        // 📌 Get flow by FlowId (for admin UI or debugging)
        Task<AutomationFlow?> GetFlowByIdAsync(Guid flowId, Guid businessId);

        // 📌 Get flow by keyword match (used for auto-triggering)
        Task<AutomationFlow?> GetFlowByKeywordAsync(Guid businessId, string keyword);

        // 🛠️ Execute a flow with contact and channel info
        Task<AutomationFlowRunResult> RunFlowAsync(
            AutomationFlow flow,
            Guid businessId,
            Guid contactId,
            string phone,
            string sourceChannel,
            string industryTag
        );

        // 📋 List all flows (for admin or dashboard)
        Task<IEnumerable<AutomationFlow>> GetAllFlowsAsync(Guid businessId);

        // ➕ Create new flow
        Task<AutomationFlow> CreateFlowAsync(Guid businessId, AutomationFlow flow);

        // ❌ Delete existing flow
        Task<bool> DeleteFlowAsync(Guid flowId, Guid businessId);

        // ⚡ Runtime entry point – called when a message arrives
        Task RunByKeywordAsync(
            string messageText,
            string phoneNumber,
            string sourceChannel = "whatsapp"
        );

        // ✅ Returns true if flow matched and executed
        Task<bool> TryRunFlowByKeywordAsync(
            Guid businessId,
            string messageText,
            string userPhone,
            string sourceChannel,
            string industryTag
        );
    }
}
