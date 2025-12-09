using System;
using System.Threading;
using System.Threading.Tasks;
using xbytechat.api.Features.CTAFlowBuilder.DTOs;
using xbytechat.api.Features.CTAFlowBuilder.Models;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    /// <summary>
    /// Runtime engine for executing CTA flows (journey flows).
    /// 
    /// This is the single entry point used by other modules:
    /// - AutoReply (CTA_FLOW nodes)
    /// - Campaigns (button-click journeys)
    /// - Future JourneyBot / Inbox actions
    /// </summary>
    public interface ICtaFlowRuntimeService
    {
        /// <summary>
        /// Starts a CTA flow journey for a given contact.
        /// </summary>
        /// <param name="businessId">Tenant business id.</param>
        /// <param name="contactId">Contact id in CRM (if known).</param>
        /// <param name="contactPhone">Contact phone number (WhatsApp).</param>
        /// <param name="configId">CTA flow config id (visual flow definition).</param>
        /// <param name="origin">Where this journey was triggered from.</param>
        /// <param name="autoReplyFlowId">
        /// Optional AutoReplyFlow id when origin = AutoReply; otherwise null.
        /// </param>
        Task<CtaFlowRunResult> StartFlowAsync(
            Guid businessId,
            Guid contactId,
            string contactPhone,
            Guid configId,
            FlowExecutionOrigin origin,
            Guid? autoReplyFlowId,
            CancellationToken cancellationToken = default);
    }
}
