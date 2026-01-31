using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.Contracts;
using xbytechat.api.Features.ESU.Facebook.DTOs;
using xbytechat.api.Features.ESU.Facebook.Options;
using xbytechat.api.Features.ESU.Facebook.Clients;
using xbytechat.api.Features.ESU.Shared;
using xbytechat.api.Features.WhatsAppSettings.Services;
using xbytechat.api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.DTOs;
using xbytechat_api.WhatsAppSettings.Services;

namespace xbytechat.api.Features.ESU.Facebook.Services
{
    internal sealed class FacebookEsuService : IFacebookEsuService
    {
        private const string Provider = "META_CLOUD";

        private readonly IOptions<EsuOptions> _options;
        private readonly IOptions<FacebookOauthOptions> _oauthOpts;
        private readonly IEsuStateStore _stateStore;
        private readonly IEsuFlagStore _flagStore;
        private readonly IFacebookOauthClient _oauth;
        private readonly IEsuTokenStore _tokens;
        private readonly IFacebookTokenService _fbTokens;
        private readonly IWhatsAppSettingsService _waSettings;
        private readonly IWhatsAppPhoneNumberService _waPhones;
        private readonly ILogger<FacebookEsuService> _log;
        private readonly IEsuStatusService _esuStatus;
        private readonly IWabaSubscriptionClient _wabaSubscription;

        public FacebookEsuService(
            IOptions<EsuOptions> options,
            IEsuStateStore stateStore,
            IEsuFlagStore flagStore,
            IFacebookOauthClient oauth,
            IEsuTokenStore tokens,
            IFacebookTokenService fbTokens,
            IWhatsAppSettingsService waSettings,
            IWhatsAppPhoneNumberService waPhones,
            IOptions<FacebookOauthOptions> oauthOpts,
            ILogger<FacebookEsuService> log,
            IEsuStatusService esuStatus,
            IWabaSubscriptionClient wabaSubscription)
        {
            _options = options;
            _stateStore = stateStore;
            _flagStore = flagStore;
            _oauth = oauth;
            _tokens = tokens;
            _fbTokens = fbTokens;
            _waSettings = waSettings;
            _waPhones = waPhones;
            _oauthOpts = oauthOpts;
            _log = log;
            _esuStatus = esuStatus;
            _wabaSubscription = wabaSubscription;
        }

        // =======================
        // ESU START
        // =======================
        public async Task<FacebookEsuStartResponseDto> StartAsync(
            Guid businessId,
            string? returnUrl,
            CancellationToken ct = default)
        {
            var cfg = _options.Value.Facebook;

            if (string.IsNullOrWhiteSpace(cfg.AppId))
                throw new InvalidOperationException("ESU.Facebook.AppId is not configured.");
            if (string.IsNullOrWhiteSpace(cfg.RedirectUri))
                throw new InvalidOperationException("ESU.Facebook.RedirectUri is not configured.");
            if (string.IsNullOrWhiteSpace(cfg.ConfigId))
                throw new InvalidOperationException("ESU.Facebook.ConfigId is not configured.");

            var state = CreateStateToken(businessId, returnUrl);
            var ttl = TimeSpan.FromMinutes(Math.Max(1, cfg.StateTtlMinutes));

            await _stateStore.StoreAsync(state, businessId, ttl);

            var dialogVersion = _oauthOpts.Value.GraphApiVersion?.Trim('/') ?? "v20.0";
            var dialogBase = $"https://www.facebook.com/{dialogVersion}/dialog/oauth";

            var query = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["client_id"] = cfg.AppId,
                ["redirect_uri"] = cfg.RedirectUri,
                ["state"] = state,
                ["response_type"] = "code",
                ["config_id"] = cfg.ConfigId,

                // ✅ IMPORTANT: Explicit scope improves reliability + App Review clarity
                ["scope"] = !string.IsNullOrWhiteSpace(cfg.Scopes)
                    ? cfg.Scopes.Trim()
                    : null
            };

            var launchUrl = QueryHelpers.AddQueryString(dialogBase, query);

            _log.LogInformation(
                "ESU Start: biz={BusinessId}, statePrefix={StatePrefix}, url={Url}",
                businessId,
                state.Length > 16 ? state[..16] : state,
                launchUrl);

            return new FacebookEsuStartResponseDto
            {
                LaunchUrl = launchUrl,
                State = state,
                ExpiresAtUtc = DateTime.UtcNow.Add(ttl)
            };
        }

        // =======================
        // ESU CALLBACK
        // =======================
        public async Task<FacebookEsuCallbackResponseDto> HandleCallbackAsync(
            string code,
            string state,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("OAuth failed: missing 'code'.");
            if (string.IsNullOrWhiteSpace(state))
                throw new InvalidOperationException("OAuth failed: missing 'state'.");

            var (found, businessId) = await _stateStore.TryConsumeAsync(state);
            if (!found || businessId == Guid.Empty)
                throw new InvalidOperationException("Invalid or expired state.");

            _log.LogInformation(
                "ESU Callback: biz={BusinessId}, statePrefix={StatePrefix}",
                businessId,
                state.Length > 16 ? state[..16] : state);

            // 1) Exchange short-lived → long-lived token
            var token = await _oauth.ExchangeCodeAsync(code, ct);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
                throw new InvalidOperationException("OAuth exchange did not return an access token.");

            token = await _oauth.ExchangeForLongLivedAsync(token, ct);

            var accessToken = token.AccessToken;
            DateTime? expiresAtUtc = (token.ExpiresInSeconds > 0)
                ? DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds)
                : (DateTime?)null;

            _log.LogInformation(
                "ESU Callback: received long-lived token for biz={BusinessId}, expiresAt={ExpiresAt}",
                businessId,
                expiresAtUtc?.ToString("O") ?? "<none>");

            await _tokens.UpsertAsync(
                businessId,
                Provider,
                accessToken,
                expiresAtUtc,
                ct);

            await _fbTokens.InvalidateAsync(businessId, ct);

            var graphBase = _oauthOpts.Value.GraphBaseUrl?.TrimEnd('/') ?? "https://graph.facebook.com";
            var graphVer = _oauthOpts.Value.GraphApiVersion?.Trim('/') ?? "v20.0";
            var apiBase = $"{graphBase}/{graphVer}";

            // 2) WABA DISCOVERY
            string? wabaId = null;

            try
            {
                var oauthCfg = _oauthOpts.Value;

                if (!string.IsNullOrWhiteSpace(oauthCfg.AppId) &&
                    !string.IsNullOrWhiteSpace(oauthCfg.AppSecret))
                {
                    var viaDebug = await TryGetWabaFromDebugTokenAsync(
                        graphBase,
                        accessToken,
                        oauthCfg.AppId,
                        oauthCfg.AppSecret,
                        ct);

                    if (!string.IsNullOrWhiteSpace(viaDebug))
                    {
                        wabaId = viaDebug;
                        _log.LogInformation(
                            "ESU Callback: WABA discovered via debug_token: {WabaId} (biz={BusinessId})",
                            wabaId,
                            businessId);
                    }
                }
                else
                {
                    _log.LogWarning(
                        "ESU Callback: AppId/AppSecret missing in FacebookOauthOptions; skipping debug_token WABA discovery (biz={BusinessId})",
                        businessId);
                }

                if (string.IsNullOrWhiteSpace(wabaId))
                {
                    wabaId = await TryGetWabaFromMeAccountsAsync(apiBase, accessToken, ct);
                }

                if (string.IsNullOrWhiteSpace(wabaId))
                {
                    wabaId = await TryGetWabaFromBusinessesAsync(apiBase, accessToken, ct);
                }

                if (string.IsNullOrWhiteSpace(wabaId))
                {
                    _log.LogWarning(
                        "ESU Callback: No WABA discovered for biz={BusinessId}. Token scopes or ESU config may be incomplete.",
                        businessId);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU Callback: Error during WABA discovery for biz={BusinessId}", businessId);
            }

            // 3) SAVE GLOBAL SETTINGS
            try
            {
                var dto = new SaveWhatsAppSettingDto
                {
                    BusinessId = businessId,
                    Provider = Provider,
                    ApiUrl = apiBase,
                    ApiKey = accessToken,
                    WabaId = string.IsNullOrWhiteSpace(wabaId) ? null : wabaId,
                    SenderDisplayName = null,
                    WebhookSecret = null,
                    WebhookVerifyToken = null,
                    WebhookCallbackUrl = null,
                    IsActive = true
                };

                await _waSettings.SaveOrUpdateSettingAsync(dto);

                _log.LogInformation(
                    "ESU Callback: WhatsApp settings saved for biz={BusinessId}, provider={Provider}, hasWaba={HasWaba}",
                    businessId,
                    Provider,
                    !string.IsNullOrWhiteSpace(wabaId));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU Callback: Failed to save WhatsApp settings for biz={BusinessId}", businessId);
            }

            // ✅ IMPORTANT: Load setting ONCE for steps 4 + subscription
            WhatsAppSettingsDto? setting = null;
            try
            {
                setting = await _waSettings.GetSettingsByBusinessIdAsync(businessId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU Callback: Failed to reload WhatsApp settings for biz={BusinessId}", businessId);
            }

            // 4) SYNC PHONE NUMBERS (best-effort, but logged)
            try
            {
                if (setting is not null &&
                    setting.Provider?.Equals(Provider, StringComparison.OrdinalIgnoreCase) == true &&
                    !string.IsNullOrWhiteSpace(setting.WabaId) &&
                    !string.IsNullOrWhiteSpace(setting.ApiKey))
                {
                    // ✅ FIX: box tuple into object so "?.ToString()" compiles
                    object syncResult = await _waPhones.SyncFromProviderAsync(businessId, setting, Provider, ct);

                    if (TryExtractCounts(syncResult, out var added, out var updated, out var total))
                    {
                        _log.LogInformation(
                            "ESU Callback: Phone sync complete for biz={BusinessId}. Added={Added}, Updated={Updated}, Total={Total}",
                            businessId, added, updated, total);
                    }
                    else
                    {
                        _log.LogInformation(
                            "ESU Callback: Phone sync complete for biz={BusinessId}. Result={Result}",
                            businessId,
                            syncResult?.ToString() ?? "<null>");
                    }
                }
                else
                {
                    _log.LogWarning(
                        "ESU Callback: Skipping phone sync for biz={BusinessId} (provider={Provider}, WabaId={WabaId}, HasApiKey={HasKey})",
                        businessId,
                        setting?.Provider,
                        setting?.WabaId ?? "<none>",
                        !string.IsNullOrWhiteSpace(setting?.ApiKey));
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU Callback: Error during phone sync for biz={BusinessId}", businessId);
            }

            // 4b) Subscribe WABA to app events
            if (setting is not null && !string.IsNullOrWhiteSpace(setting.WabaId))
            {
                try
                {
                    await SubscribeWabaAsync(setting.WabaId!, accessToken, ct);

                    _log.LogInformation(
                        "ESU Callback: WABA subscribed successfully. biz={BusinessId}, wabaId={WabaId}",
                        businessId, setting.WabaId);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex,
                        "ESU Callback: WABA subscribe failed (non-blocking). businessId={BusinessId}, wabaId={WabaId}",
                        businessId, setting.WabaId);
                }
            }

            // 5) FLAG AS COMPLETED
            try
            {
                var payloadJson = JsonSerializer.Serialize(new
                {
                    completed = true,
                    provider = Provider,
                    expires_at_utc = expiresAtUtc
                });

                await _flagStore.UpsertAsync(
                    businessId,
                    key: "facebook.esu",
                    value: "completed",
                    jsonPayload: payloadJson,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU Callback: Failed to write ESU completion flag for biz={BusinessId}", businessId);
            }

            // 6) FINAL REDIRECT
            var rawReturnUrl = TryGetReturnUrlFromState(state);
            var redirectBase = SanitizeReturnUrlOrDefault(rawReturnUrl, "/app/welcomepage");

            var redirect = redirectBase.Contains("?")
                ? $"{redirectBase}&esuStatus=success"
                : $"{redirectBase}?esuStatus=success";

            return new FacebookEsuCallbackResponseDto { RedirectTo = redirect };
        }

        // =======================
        // DISCONNECT
        // =======================
        public async Task DisconnectAsync(Guid businessId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(businessId));

            _log.LogInformation("ESU Disconnect: biz={BusinessId}", businessId);

            bool hadEsuOrToken = false;

            object? statusObj = null;
            try
            {
                statusObj = await _esuStatus.GetStatusAsync(businessId, ct);
                if (statusObj is not null)
                {
                    hadEsuOrToken = ReadBool(statusObj, "Connected")
                                    || ReadBool(statusObj, "HasEsuFlag")
                                    || ReadBool(statusObj, "HasValidToken");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU Disconnect: failed to read status for biz={BusinessId}; continuing.", businessId);
            }

            if (statusObj is not null && (ReadBool(statusObj, "HardDeleted") || ReadBool(statusObj, "IsHardDeleted")))
            {
                _log.LogInformation("ESU Disconnect: biz={BusinessId} is hard-deleted; skipping disconnect to avoid re-creating data.", businessId);
                return;
            }

            if (!hadEsuOrToken)
            {
                _log.LogInformation(
                    "ESU Disconnect: nothing to disconnect for biz={BusinessId} (no ESU flags/tokens/settings).",
                    businessId);
                return;
            }

            // 1) Remote revoke (best-effort)
            try
            {
                var t = await _tokens.GetAsync(businessId, Provider, ct);
                if (t is not null && !string.IsNullOrWhiteSpace(t.AccessToken) && !t.IsRevoked)
                {
                    var graphBase = _oauthOpts.Value.GraphBaseUrl?.TrimEnd('/') ?? "https://graph.facebook.com";
                    var graphVer = _oauthOpts.Value.GraphApiVersion?.Trim('/') ?? "v20.0";
                    var apiBase = $"{graphBase}/{graphVer}";

                    // 1a) Unsubscribe WABA from app (CRITICAL for clean re-signup)
                    try 
                    {
                        var setting = await _waSettings.GetSettingsByBusinessIdAsync(businessId);
                        if (setting is not null && !string.IsNullOrWhiteSpace(setting.WabaId))
                        {
                            _log.LogInformation("ESU Disconnect: attempting Meta WABA unsubscribe for biz={BusinessId}, waba={WabaId}", 
                                businessId, setting.WabaId);
                            
                            await _wabaSubscription.UnsubscribeAsync(setting.WabaId, t.AccessToken, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "ESU Disconnect: Meta WABA unsubscribe failed (non-blocking) for biz={BusinessId}", businessId);
                    }

                    // 1b) Revoke permissions
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", t.AccessToken);

                    var resp = await http.DeleteAsync($"{apiBase}/me/permissions", ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _log.LogWarning(
                            "ESU Disconnect: remote revoke returned {Status} for biz={BusinessId}",
                            resp.StatusCode, businessId);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU Disconnect: error during remote revoke for biz={BusinessId}", businessId);
            }

            // 2) Canonical local deauthorize
            try
            {
                await _esuStatus.DeauthorizeAsync(businessId, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU Disconnect: error during local deauthorize for biz={BusinessId}", businessId);
            }

            // 3) Deactivate WhatsApp settings
            try
            {
                var existing = await _waSettings.GetSettingsByBusinessIdAsync(businessId);
                if (existing is not null)
                {
                    await _waSettings.SaveOrUpdateSettingAsync(new SaveWhatsAppSettingDto
                    {
                        BusinessId = businessId,
                        Provider = Provider,
                        ApiUrl = null,
                        ApiKey = null,
                        WabaId = null,
                        SenderDisplayName = null,
                        WebhookSecret = null,
                        WebhookVerifyToken = null,
                        WebhookCallbackUrl = null,
                        IsActive = false
                    });
                }
                else
                {
                    _log.LogInformation(
                        "ESU Disconnect: WhatsApp settings already absent for biz={BusinessId}; skipping deactivation to avoid recreating rows.",
                        businessId);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ESU Disconnect: error deactivating WhatsApp settings for biz={BusinessId}", businessId);
            }

            // 4) UX flag
            try
            {
                await _flagStore.UpsertAsync(
                    businessId,
                    key: "facebook.esu",
                    value: "disconnected",
                    jsonPayload: "{\"completed\":false}",
                    ct: ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU Disconnect: failed to update ESU 'disconnected' flag for biz={BusinessId}", businessId);
            }
        }

        // =======================
        // FULL DELETE (hard delete)
        // =======================
        public async Task FullDeleteAsync(Guid businessId, CancellationToken ct = default)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(businessId));

            _log.LogInformation("ESU FullDelete: start for biz={BusinessId}", businessId);

            try
            {
                await DisconnectAsync(businessId, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "ESU FullDelete: DisconnectAsync failed or partial for biz={BusinessId}. Continuing with local cleanup.",
                    businessId);
            }

            try
            {
                await _tokens.DeleteAsync(businessId, Provider, ct);
                _log.LogInformation("ESU FullDelete: EsuTokens deleted for biz={BusinessId}", businessId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU FullDelete: failed to delete EsuTokens for biz={BusinessId}", businessId);
            }

            try
            {
                var deleted = await _waSettings.DeleteSettingsAsync(businessId, ct);
                _log.LogInformation("ESU FullDelete: WhatsApp settings+phones delete={Deleted} for biz={BusinessId}", deleted, businessId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU FullDelete: failed to delete WhatsApp settings for biz={BusinessId}", businessId);
            }

            try
            {
                await _flagStore.DeleteAsync(businessId, ct);
                _log.LogInformation("ESU FullDelete: IntegrationFlags row deleted for biz={BusinessId}", businessId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU FullDelete: failed to delete IntegrationFlags for biz={BusinessId}", businessId);
            }

            try
            {
                await _fbTokens.InvalidateAsync(businessId, ct);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "ESU FullDelete: token cache invalidate failed for biz={BusinessId}", businessId);
            }

            _log.LogInformation("ESU FullDelete: completed for biz={BusinessId}", businessId);
        }

        // =======================
        // REGISTER NUMBER
        // =======================
        public async Task RegisterPhoneNumberAsync(Guid businessId, string pin, CancellationToken ct)
        {
            if (businessId == Guid.Empty)
                throw new ArgumentException("BusinessId is required.", nameof(businessId));

            pin = (pin ?? string.Empty).Trim();

            if (pin.Length != 6 || !IsAllDigits(pin))
                throw new InvalidOperationException("PIN must be exactly 6 digits.");

            var setting = await _waSettings.GetSettingsByBusinessIdAsync(businessId);
            if (setting is null)
                throw new InvalidOperationException("WhatsApp settings not found for this business. Please connect ESU first.");

            if (!string.Equals(setting.Provider, Provider, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Invalid provider. Expected '{Provider}' but got '{setting.Provider}'.");

            if (string.IsNullOrWhiteSpace(setting.ApiUrl) || string.IsNullOrWhiteSpace(setting.ApiKey))
                throw new InvalidOperationException("WhatsApp settings are missing ApiUrl/ApiKey. Please reconnect ESU.");

            var phones = await _waPhones.ListAsync(businessId, Provider, ct);
            if (phones is null || phones.Count == 0)
                throw new InvalidOperationException("No phone numbers found. Please complete ESU and phone sync first.");

            var phone = phones[0];
            var phoneNumberId = GetPhoneNumberId(phone);

            if (string.IsNullOrWhiteSpace(phoneNumberId))
                throw new InvalidOperationException("PhoneNumberId is missing in stored phone record. Sync did not capture it.");

            var url = $"{setting.ApiUrl.TrimEnd('/')}/{phoneNumberId}/register";

            var payload = new
            {
                messaging_product = "whatsapp",
                pin = pin
            };

            _log.LogInformation(
                "ESU RegisterNumber: registering phone_number_id={PhoneNumberId} for biz={BusinessId}",
                phoneNumberId,
                businessId);

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiKey);

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };

            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "ESU RegisterNumber failed: biz={BusinessId}, phone_number_id={PhoneNumberId}, status={Status}, body={Body}",
                    businessId,
                    phoneNumberId,
                    (int)res.StatusCode,
                    Truncate(body));

                throw new InvalidOperationException($"Failed to register phone number. Meta returned {(int)res.StatusCode}.");
            }

            _log.LogInformation(
                "ESU RegisterNumber success: biz={BusinessId}, phone_number_id={PhoneNumberId}, body={Body}",
                businessId,
                phoneNumberId,
                Truncate(body));

            // Best-effort: resync after register
            try
            {
                // ✅ FIX: box tuple into object so "?.ToString()" compiles
                object syncResult = await _waPhones.SyncFromProviderAsync(businessId, setting, Provider, ct);

                if (TryExtractCounts(syncResult, out var added, out var updated, out var total))
                {
                    _log.LogInformation(
                        "ESU RegisterNumber: phone sync after register complete for biz={BusinessId}. Added={Added}, Updated={Updated}, Total={Total}",
                        businessId, added, updated, total);
                }
                else
                {
                    _log.LogInformation(
                        "ESU RegisterNumber: phone sync after register complete for biz={BusinessId}. Result={Result}",
                        businessId,
                        syncResult?.ToString() ?? "<null>");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "ESU RegisterNumber: sync-after-register failed for biz={BusinessId} (non-blocking).",
                    businessId);
            }
        }

        // =======================
        // HELPERS
        // =======================
        private static string CreateStateToken(Guid businessId, string? returnUrl)
        {
            Span<byte> random = stackalloc byte[16];
            RandomNumberGenerator.Fill(random);
            var payload =
                $"{businessId:N}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{Convert.ToHexString(random)}|{(returnUrl ?? "")}";
            var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        private static string? TryGetReturnUrlFromState(string state)
        {
            try
            {
                var bytes = WebEncoders.Base64UrlDecode(state);
                var payload = System.Text.Encoding.UTF8.GetString(bytes);
                var parts = payload.Split('|', 4, StringSplitOptions.None);
                if (parts.Length == 4)
                {
                    var url = parts[3];
                    return string.IsNullOrWhiteSpace(url) ? null : url;
                }
            }
            catch { }
            return null;
        }

        private async Task SubscribeWabaAsync(string wabaId, string accessToken, CancellationToken ct)
        {
            var graphBase = _oauthOpts.Value.GraphBaseUrl?.TrimEnd('/') ?? "https://graph.facebook.com";
            var graphVer = _oauthOpts.Value.GraphApiVersion?.Trim('/') ?? "v20.0";
            var url = $"{graphBase}/{graphVer}/{wabaId}/subscribed_apps";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var res = await http.PostAsync(url, content: null, ct);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"WABA subscribe failed ({(int)res.StatusCode}). Body: {Truncate(body)}");
            }
        }

        private async Task<string?> TryGetWabaFromDebugTokenAsync(
            string graphBase,
            string inputToken,
            string appId,
            string appSecret,
            CancellationToken ct)
        {
            try
            {
                var appToken = $"{appId}|{appSecret}";
                var url = $"{graphBase.TrimEnd('/')}/debug_token" +
                          $"?input_token={Uri.EscapeDataString(inputToken)}" +
                          $"&access_token={Uri.EscapeDataString(appToken)}";

                using var http = new HttpClient();
                var json = await http.GetStringAsync(url, ct);

                _log.LogDebug("ESU debug_token raw={Body}", Truncate(json));

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("data", out var data)) return null;

                if (data.TryGetProperty("granular_scopes", out var scopes) &&
                    scopes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in scopes.EnumerateArray())
                    {
                        var scope = s.TryGetProperty("scope", out var se) ? se.GetString() : null;
                        if (string.IsNullOrWhiteSpace(scope)) continue;

                        if (scope.StartsWith("whatsapp_business_", StringComparison.OrdinalIgnoreCase))
                        {
                            if (s.TryGetProperty("target_ids", out var targets) &&
                                targets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in targets.EnumerateArray())
                                {
                                    var id = t.GetString();
                                    if (!string.IsNullOrWhiteSpace(id))
                                        return id;
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ESU debug_token WABA discovery failed.");
                return null;
            }
        }

        private async Task<string?> TryGetWabaFromMeAccountsAsync(
            string apiBase,
            string accessToken,
            CancellationToken ct)
        {
            var url = $"{apiBase}/me/whatsapp_business_accounts?fields=id,name";
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var res = await http.GetAsync(url, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            _log.LogDebug("ESU me/whatsapp_business_accounts: {Status} {Body}",
                (int)res.StatusCode,
                Truncate(body));

            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var it in arr.EnumerateArray())
            {
                if (it.TryGetProperty("id", out var idp))
                {
                    var id = idp.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                        return id;
                }
            }

            return null;
        }

        private async Task<string?> TryGetWabaFromBusinessesAsync(
            string apiBase,
            string accessToken,
            CancellationToken ct)
        {
            var url =
                $"{apiBase}/me/businesses?fields=owned_whatsapp_business_accounts{{id}},client_whatsapp_business_accounts{{id}}";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var res = await http.GetAsync(url, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            _log.LogDebug("ESU me/businesses: {Status} {Body}",
                (int)res.StatusCode,
                Truncate(body));

            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var biz in arr.EnumerateArray())
            {
                if (TryPickWabaId(biz, "owned_whatsapp_business_accounts", out var ow) &&
                    !string.IsNullOrWhiteSpace(ow))
                    return ow;

                if (TryPickWabaId(biz, "client_whatsapp_business_accounts", out var cl) &&
                    !string.IsNullOrWhiteSpace(cl))
                    return cl;
            }

            return null;
        }

        private static string Truncate(string? s, int max = 600)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s[..max] + "...";
        }

        private static bool TryPickWabaId(JsonElement biz, string prop, out string? wabaId)
        {
            wabaId = null;
            if (!biz.TryGetProperty(prop, out var block)) return false;
            if (!block.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array) return false;

            foreach (var it in arr.EnumerateArray())
            {
                if (it.TryGetProperty("id", out var idp))
                {
                    wabaId = idp.GetString();
                    if (!string.IsNullOrWhiteSpace(wabaId))
                        return true;
                }
            }

            return false;
        }

        private static string SanitizeReturnUrlOrDefault(string? returnUrl, string fallback)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
                return fallback;

            returnUrl = returnUrl.Trim();

            if (!returnUrl.StartsWith("/", StringComparison.Ordinal) ||
                returnUrl.StartsWith("//", StringComparison.Ordinal) ||
                returnUrl.Contains("\\", StringComparison.Ordinal))
            {
                return fallback;
            }

            return returnUrl;
        }

        private static bool IsAllDigits(string value)
        {
            foreach (var c in value)
            {
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        private static string? GetPhoneNumberId(object phone)
        {
            var type = phone.GetType();

            var prop =
                type.GetProperty("ProviderPhoneNumberId")
                ?? type.GetProperty("PhoneNumberId")
                ?? type.GetProperty("MetaPhoneNumberId")
                ?? type.GetProperty("ProviderId");

            return prop?.GetValue(phone)?.ToString();
        }

        private static bool TryExtractCounts(object? result, out int added, out int updated, out int total)
        {
            added = updated = total = 0;
            if (result is null) return false;

            var t = result.GetType();

            var pAdded = t.GetProperty("Added");
            var pUpdated = t.GetProperty("Updated");
            var pTotal = t.GetProperty("Total");

            if (pAdded is not null && pUpdated is not null && pTotal is not null)
            {
                try
                {
                    added = Convert.ToInt32(pAdded.GetValue(result));
                    updated = Convert.ToInt32(pUpdated.GetValue(result));
                    total = Convert.ToInt32(pTotal.GetValue(result));
                    return true;
                }
                catch { }
            }

            var f1 = t.GetField("Item1");
            var f2 = t.GetField("Item2");
            var f3 = t.GetField("Item3");

            if (f1 is not null && f2 is not null && f3 is not null)
            {
                try
                {
                    added = Convert.ToInt32(f1.GetValue(result));
                    updated = Convert.ToInt32(f2.GetValue(result));
                    total = Convert.ToInt32(f3.GetValue(result));
                    return true;
                }
                catch { }
            }

            return false;
        }

        private static bool ReadBool(object obj, string propName)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p is null) return false;
                var val = p.GetValue(obj);
                return val is bool b && b;
            }
            catch
            {
                return false;
            }
        }
    }
}
