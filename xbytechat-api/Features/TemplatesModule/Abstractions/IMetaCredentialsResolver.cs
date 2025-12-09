namespace xbytechat.api.Features.TemplateModule.Abstractions;

public sealed record MetaCredentials(
    string AccessToken,
    string GraphBaseUrl,
    string GraphVersion,
    string WabaId
);

public interface IMetaCredentialsResolver
{
    /// <summary>
    /// Resolve access token, graph base/version, and WABA ID for a business.
    /// Implement this by reading your WhatsAppSettings (active provider row).
    /// </summary>
    Task<MetaCredentials> ResolveAsync(Guid businessId, CancellationToken ct = default);
}
