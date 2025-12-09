using Serilog;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.Tracking.Models;

namespace xbytechat.api.Features.CTAFlowBuilder.Services
{
    public static class StepMatchingHelper
    {
        public static bool IsStepMatched(CTAFlowStep step, TrackingLog log, Contact? contact)
        {
            if (!string.IsNullOrWhiteSpace(step.RequiredSource) &&
                !string.Equals(step.RequiredSource, log.SourceType, StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("🚫 Step [{StepId}] skipped: RequiredSource '{Required}' ≠ ClickedSource '{Actual}'",
                    step.Id, step.RequiredSource, log.SourceType);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(step.RequiredTag))
            {
                if (contact == null || contact.ContactTags == null || !contact.ContactTags.Any())
                {
                    Log.Information("🚫 Step [{StepId}] skipped: Contact or Tags missing (RequiredTag: {RequiredTag})",
                        step.Id, step.RequiredTag);
                    return false;
                }

                var hasTag = contact.ContactTags.Any(ct =>
                    string.Equals(ct.Tag.Name, step.RequiredTag, StringComparison.OrdinalIgnoreCase));

                if (!hasTag)
                {
                    var contactTags = string.Join(", ", contact.ContactTags.Select(ct => ct.Tag.Name));
                    Log.Information("🚫 Step [{StepId}] skipped: Contact tags [{Tags}] do not include RequiredTag '{Required}'",
                        step.Id, contactTags, step.RequiredTag);
                    return false;
                }
            }

            return true;
        }

    }
}
