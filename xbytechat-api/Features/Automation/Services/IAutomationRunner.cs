using System;
using System.Threading.Tasks;
using xbytechat.api.Features.Automation.Models;

namespace xbytechat.api.Features.Automation.Services
{
    public interface IAutomationRunner
    {
        Task<AutomationFlowRunResult> RunFlowAsync(
             AutomationFlow flow,
             Guid businessId,
             Guid contactId,
             string contactPhone,
             string sourceChannel,
             string industryTag
 );

    }
}

