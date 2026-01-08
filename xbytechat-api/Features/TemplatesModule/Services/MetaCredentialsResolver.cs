using System.Text.RegularExpressions;
using xbytechat_api.WhatsAppSettings.Services; // IWhatsAppSettingsService
using xbytechat.api.Features.TemplateModule.Abstractions;

namespace xbytechat.api.Features.TemplateModule.Services;

public sealed class MetaCredentialsResolver : IMetaCredentialsResolver
{
    private readonly IWhatsAppSettingsService _wa;

    // Matches .../vXX or .../vXX.X (optionally with trailing slash)
    private static readonly Regex VersionSeg = new(@"/(v\d+(?:\.\d+)?)\/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MetaCredentialsResolver(IWhatsAppSettingsService waSettings)
    {
        _wa = waSettings;
    }

    public async Task<MetaCredentials> ResolveAsync(Guid businessId, CancellationToken ct = default)
    {
        // Your existing call; if you have a ct-aware version, wire it here.
        var s = await _wa.GetSettingsByBusinessIdAsync(businessId);
        if (s is null) throw new InvalidOperationException("WhatsApp settings not found for this business.");

        var apiUrl = (s.ApiUrl ?? string.Empty).Trim().TrimEnd('/');
        var token = (s.ApiKey ?? string.Empty).Trim();
        var wabaId = (s.WabaId ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(apiUrl)) throw new InvalidOperationException("Meta ApiUrl is missing.");
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Meta access token is missing.");
        if (string.IsNullOrWhiteSpace(wabaId)) throw new InvalidOperationException("Meta WABA ID is missing.");

        // If ApiUrl already includes a version (e.g., https://graph.facebook.com/v21.0)
        // split it into base + version; otherwise leave version empty.
        string baseUrl = apiUrl;
        string version = string.Empty;

        var m = VersionSeg.Match(apiUrl);
        if (m.Success)
        {
            version = m.Groups[1].Value;                                // e.g., v21.0
            baseUrl = apiUrl.Substring(0, m.Index).TrimEnd('/');        // e.g., https://graph.facebook.com
        }

        return new MetaCredentials(
            AccessToken: token,
            GraphBaseUrl: baseUrl,
            GraphVersion: version,   // can be "" if ApiUrl had no version
            WabaId: wabaId,
            PhoneNumberId: s.PhoneNumberId
        );
    }
}
