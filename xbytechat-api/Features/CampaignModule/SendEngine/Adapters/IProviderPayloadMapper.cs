namespace xbytechat.api.Features.CampaignModule.SendEngine;

public interface IProviderPayloadMapper
{
    /// Maps our provider-agnostic envelope into the wire payload object the engine expects.
    object BuildPayload(SendPlan plan, RecipientPlan r, TemplateEnvelope env);
}
