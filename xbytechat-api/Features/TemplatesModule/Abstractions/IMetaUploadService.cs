namespace xbytechat.api.Features.TemplateModule.Abstractions;

public enum HeaderMediaType { IMAGE, VIDEO, DOCUMENT }

public sealed record HeaderUploadResult(
    string Handle,        // raw handle, e.g. "4::abc..."
    string MimeType,
    long SizeBytes,
    bool IsStub         // true if returned by stub mode
);

public interface IMetaUploadService
{
    /// <summary>
    /// Upload a header media to Meta (resumable) and return the asset handle.
    /// If sourceUrl is provided, the service will download and upload it.
    /// Exactly one of (fileStream, sourceUrl) must be provided.
    /// </summary>
    Task<HeaderUploadResult> UploadHeaderAsync(
        Guid businessId,
        HeaderMediaType mediaType,
        Stream? fileStream,
        string? fileName,
        string? sourceUrl,
        CancellationToken ct = default);
}
