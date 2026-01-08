namespace xbytechat.api.Features.TemplateModule.Abstractions;

public interface IMetaTemplateClient
{
    Task<string> UploadMediaAsync(Guid businessId, string localPathOrUrl, string mediaType, CancellationToken ct = default);
    Task<MetaTemplateCallResult> CreateTemplateAsync(Guid businessId, string name, string category, string language, object componentsPayload, object examplesPayload, CancellationToken ct = default);
    Task<int> SyncTemplatesAsync(Guid businessId, CancellationToken ct = default);
    Task<bool> DeleteTemplateAsync(Guid businessId, string name, string language, CancellationToken ct = default);
    Task<(Stream? ValidStream, string? ContentType)> GetMediaStreamAsync(Guid businessId, string mediaId, CancellationToken ct = default);
}

public sealed record MetaTemplateCallResult(bool Success, string? Error);
