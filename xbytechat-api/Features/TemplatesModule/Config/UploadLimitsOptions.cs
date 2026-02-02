namespace xbytechat.api.Features.TemplateModule.Config;

public sealed class UploadLimitsOptions
{
    public long ImageMaxBytes { get; set; } = 10 * 1024 * 1024;    // 10 MB
    public long VideoMaxBytes { get; set; } = 16 * 1024 * 1024;    // 16 MB
    public long DocumentMaxBytes { get; set; } = 16 * 1024 * 1024; // 16 MB

    public string[] AllowedImageMime { get; set; } = new[] { "image/jpeg", "image/png", "image/webp" };
    public string[] AllowedVideoMime { get; set; } = new[] { "video/mp4", "video/3gpp" };
    public string[] AllowedDocMime { get; set; } = new[] { "application/pdf" };

    /// <summary>
    /// If true, the upload service returns a generated fake handle instead of calling Meta.
    /// Flip to false when you implement the real HTTP resumable flow.
    /// </summary>
    public bool UseStubHandle { get; set; } = false;
}
