using xbytechat.api.Features.CTAFlowBuilder.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    public interface ICTAFlowService
    {
        // Create-only (draft)
        Task<ResponseResult> SaveVisualFlowAsync(SaveVisualFlowDto dto, Guid businessId, string createdBy);

        // Load flows (lists)
        Task<List<VisualFlowSummaryDto>> GetAllPublishedFlowsAsync(Guid businessId);
        Task<List<VisualFlowSummaryDto>> GetAllDraftFlowsAsync(Guid businessId);

        // Load flow (detail)
        Task<SaveVisualFlowDto?> GetVisualFlowByIdAsync(Guid flowId, Guid businessId);  // for editor/view
        Task<ResponseResult> GetVisualFlowAsync(Guid flowId, Guid businessId);          // alt payload

        // Runtime
        Task<CTAFlowStep?> MatchStepByButtonAsync(Guid businessId, string buttonText, string buttonType, string currentTemplateName, Guid? campaignId = null);
        Task<ResponseResult> ExecuteVisualFlowAsync(Guid businessId, Guid startStepId, Guid trackingLogId, Guid? campaignSendLogId);
        Task<CTAFlowStep?> GetChainedStepAsync(Guid businessId, Guid? nextStepId);
        Task<CTAFlowStep?> GetChainedStepWithContextAsync(Guid businessId, Guid? nextStepId, Guid? trackingLogId);
        Task<FlowButtonLink?> GetLinkAsync(Guid flowId, Guid sourceStepId, short buttonIndex);

        // Delete (only if not attached)
        Task<ResponseResult> DeleteFlowAsync(Guid flowId, Guid businessId, string deletedBy);

        // Publish
        Task<bool> PublishFlowAsync(Guid flowId, Guid businessId, string user);

        // Attached campaigns (for usage checks / modal)
        Task<IReadOnlyList<AttachedCampaignDto>> GetAttachedCampaignsAsync(Guid flowId, Guid businessId);

        // (Optional utility)
        Task<bool> HardDeleteFlowIfUnusedAsync(Guid flowId, Guid businessId);
    }
}


//using xbytechat.api.Features.CTAFlowBuilder.DTOs;
//using xbytechat.api.Features.CTAFlowBuilder.Models;
//using xbytechat.api.Helpers;

//namespace xbytechat.api.Features.CTAFlowBuilder.Services
//{
//    public interface ICTAFlowService
//    {
//        // ✅ Used for flow creation and saving
//        Task<Guid> CreateFlowWithStepsAsync(CreateFlowDto dto, Guid businessId, string createdBy);
//        Task<ResponseResult> SaveVisualFlowAsync(SaveVisualFlowDto dto, Guid businessId, string createdBy);

//        // ✅ Load flows
//        Task<CTAFlowConfig?> GetFlowByBusinessAsync(Guid businessId);
//        Task<CTAFlowConfig?> GetDraftFlowByBusinessAsync(Guid businessId);
//        Task<List<VisualFlowSummaryDto>> GetAllPublishedFlowsAsync(Guid businessId);
//        Task<List<VisualFlowSummaryDto>> GetAllDraftFlowsAsync(Guid businessId);

//        // ✅ Load and manage flow steps
//        Task<List<CTAFlowStep>> GetStepsForFlowAsync(Guid flowId);


//        Task<CTAFlowStep?> MatchStepByButtonAsync(Guid businessId, string buttonText,string buttonType,string currentTemplateName,Guid? campaignId = null);


//        Task<CTAFlowStep?> GetChainedStepAsync(Guid businessId, Guid? nextStepId);
//        Task<CTAFlowStep?> GetChainedStepWithContextAsync(Guid businessId, Guid? nextStepId, Guid? trackingLogId);
//        // ✅ Runtime logic
//        Task<ResponseResult> ExecuteFollowUpStepAsync(Guid businessId, CTAFlowStep? currentStep, string recipientNumber);

//        // ✅ Flow management
//        Task<ResponseResult> PublishFlowAsync(Guid businessId, List<FlowStepDto> steps, string createdBy);

//        Task<ResponseResult> DeleteFlowAsync(Guid flowId, Guid businessId, string deletedBy);

//        // ✅ Editor loading (visual builder)
//       // Task<SaveVisualFlowDto?> GetVisualFlowByIdAsync(Guid id, Guid businessId);
//        Task<SaveVisualFlowDto?> GetVisualFlowByIdAsync(Guid flowId, Guid businessId);
//        Task<ResponseResult> GetVisualFlowAsync(Guid flowId, Guid businessId);
//        Task<ResponseResult> ExecuteVisualFlowAsync(Guid businessId, Guid startStepId, Guid trackingLogId, Guid? campaignSendLogId);
//        Task<FlowButtonLink?> GetLinkAsync(Guid flowId, Guid sourceStepId, short buttonIndex);

//        public interface IFlowRuntimeService
//        {
//            Task<NextStepResult> ExecuteNextAsync(NextStepContext context);
//        }
//        Task<IReadOnlyList<AttachedCampaignDto>> GetAttachedCampaignsAsync(Guid flowId, Guid businessId);
//        Task<bool> HardDeleteFlowIfUnusedAsync(Guid flowId, Guid businessId);
//        //Task<FlowUpdateResult> UpdateVisualFlowAsync(Guid flowId, SaveVisualFlowDto dto, Guid businessId, string user);

//        // Explicit publish after edits
//        Task<bool> PublishFlowAsync(Guid flowId, Guid businessId, string user);

//        // Create a new draft copy when live flow is attached
//        Task<Guid> ForkFlowAsync(Guid flowId, Guid businessId, string user);
//    }
//}


