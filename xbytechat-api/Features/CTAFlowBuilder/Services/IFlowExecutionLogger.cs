using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.AutoReplyBuilder.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Models;


namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    /// <summary>
    /// Abstraction for writing origin-tagged FlowExecutionLog rows.
    /// Different engines (Campaign, AutoReply, Inbox, JourneyBot)
    /// will call this with a FlowExecutionContext.
    /// </summary>
    public interface IFlowExecutionLogger
    {
        /// <summary>
        /// Persist a single step execution into FlowExecutionLogs.
        /// </summary>
        Task LogStepAsync(FlowExecutionContext context, CancellationToken cancellationToken = default);
    }
}
