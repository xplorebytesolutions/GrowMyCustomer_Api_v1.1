using System.Threading.Tasks;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;
using xbytechat.api.Features.AutoReplyBuilder.Models;

namespace xbytechat.api.Features.AutoReplyBuilder.Flows.Repositories
{
    public interface IAutoReplyFlowRepository
    {
        Task<AutoReplyFlow> SaveAsync(AutoReplyFlow flow);
        Task<AutoReplyFlow?> GetByIdAsync(Guid flowId, Guid businessId);
        Task<List<AutoReplyFlow>> GetAllByBusinessIdAsync(Guid businessId);
        Task<int> GetFlowCountAsync(Guid businessId);
        Task<bool> RenameFlowAsync(Guid id, string newName);
        Task<bool> DeleteFlowAsync(Guid id, Guid businessId);
        Task SaveNodesAndEdgesAsync(IEnumerable<AutoReplyFlowNode> nodes, IEnumerable<AutoReplyFlowEdge> edges);
        Task<List<AutoReplyFlowNode>> GetNodesByFlowIdAsync(Guid flowId);
        Task<List<AutoReplyFlowEdge>> GetEdgesByFlowIdAsync(Guid flowId);
        Task<AutoReplyFlow?> FindFlowByKeywordAsync(Guid businessId, string keyword);
        Task<List<AutoReplyFlowNode>> GetStructuredNodesAsync(Guid flowId);
        Task<List<AutoReplyFlowEdge>> GetStructuredEdgesAsync(Guid flowId);

    }
}
