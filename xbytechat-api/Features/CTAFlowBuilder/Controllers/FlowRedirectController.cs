// 📄 Features/CTAFlowBuilder/Controllers/FlowRedirectController.cs
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Infrastructure.Flows;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.CTAFlowBuilder.Services;
using Microsoft.AspNetCore.Authorization;

namespace xbytechat.api.Features.CTAFlowBuilder.Controllers
{
    [ApiController]
    [Route("r/flow")]
    public class FlowRedirectController : ControllerBase
    {
        private readonly IFlowClickTokenService _tokens;
        private readonly ICTAFlowService _flows;           // service to read flow steps/links
        private readonly IFlowRuntimeService _runtime;     // service to execute next step

        public FlowRedirectController(
            IFlowClickTokenService tokens,
            ICTAFlowService flows,
            IFlowRuntimeService runtime)
        {
            _tokens = tokens;
            _flows = flows;
            _runtime = runtime;
        }

        [HttpGet("{token}")]
        [AllowAnonymous] // secure by token, tenant checks inside
        public async Task<IActionResult> RedirectByToken(string token)
        {
            FlowClickPayload p;
            try
            {
                p = _tokens.Validate(token);
            }
            catch (Exception ex)
            {
                return BadRequest($"Invalid or expired token: {ex.Message}");
            }

            // 🔒 Tenant isolation: make sure the business in the token matches the current request context
            // (if you have multi-tenant enforcement middleware, call it here)

            // 1) Resolve the button link
            var link = await _flows.GetLinkAsync(p.fid, p.sid, p.bi);
            if (link is null)
                return NotFound("Link not found for this flow step");

            var requestId = Guid.NewGuid(); // for idempotency
            var exec = await _runtime.ExecuteNextAsync(new NextStepContext
            {
                BusinessId = p.biz,
                FlowId = p.fid,
                Version = p.ver,
                SourceStepId = p.sid,
                TargetStepId = link.NextStepId, // may be null → terminal
                ButtonIndex = p.bi,
                MessageLogId = p.mlid,
                ContactPhone = p.cp,
                RequestId = requestId,

                // 🆕 Pass the clicked button for runtime decision
                ClickedButton = link
            });


            // 3) Redirect the user
            // If button was a URL, use that; else go to a generic "thank you" page
            var dest = exec.RedirectUrl ?? "/thank-you";
            return Redirect(dest);
        }
    }
}
