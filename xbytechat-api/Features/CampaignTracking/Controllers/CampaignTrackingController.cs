// 📄 Features/CampaignTracking/Controllers/CampaignTrackingController.cs
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;
using xbytechat.api.Features.CampaignTracking.Services;
using xbytechat.api.Features.CampaignTracking.Worker;

namespace xbytechat.api.Features.CampaignTracking.Controllers
{
    [ApiController]
    [Route("r")] // /r/{token}
    public class CampaignTrackingController : ControllerBase
    {
        private static readonly HtmlEncoder HtmlEnc = HtmlEncoder.Default;

        private readonly ILogger<CampaignTrackingController> _log;
        private readonly IClickTokenService _token;
        private readonly IClickEventQueue _queue;
        private readonly AppDbContext _db;

        public CampaignTrackingController(
            ILogger<CampaignTrackingController> log,
            IClickTokenService token,
            IClickEventQueue queue,
            AppDbContext db)
        {
            _log = log;
            _token = token;
            _queue = queue;
            _db = db;
        }

        [HttpGet("{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> RedirectByToken([FromRoute] string token, CancellationToken ct)
        {
            // 1) Validate token
            if (!_token.TryValidate(token, out var p, out var reason))
            {
                _log.LogWarning("Tracking token rejected. reason={Reason}", reason);
                return BadRequest("Invalid token.");
            }

            // 2) Normalize + classify destination
            if (!TryNormalizeAllowedDestination(p!.to, out var safeDest, out var scheme))
            {
                _log.LogWarning("Rejected destination for cid {Cid}: {Dest}", p.cid, p.to);
                return BadRequest("Invalid destination.");
            }

            // 3) Capture client info
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var ua = Request.Headers.UserAgent.ToString();
            var now = DateTime.UtcNow;

            // 4) Determine click type (web | call | whatsapp)
            var clickType = ClassifyClickType(safeDest, scheme);

            // 4.1) Fetch related ids from the send log (ContactId, CampaignId, RunId)
            Guid? contactId = null;
            Guid? campaignId = null; // keep nullable locally
            Guid? runId = null;

            try
            {
                var sendLog = await _db.CampaignSendLogs
                    .AsNoTracking()
                    .Where(x => x.Id == p.cid)
                    .Select(x => new { x.ContactId, x.CampaignId, x.RunId })
                    .FirstOrDefaultAsync(ct);

                if (sendLog is not null)
                {
                    contactId = sendLog.ContactId;
                    campaignId = sendLog.CampaignId;
                    runId = sendLog.RunId;
                }
                else
                {
                    _log.LogWarning("SendLog not found for click cid={Cid}", p.cid);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to fetch ContactId/CampaignId/RunId for cid={Cid}", p.cid);
            }

            // 5) Write-through (best-effort, but executed inline)
            try
            {
                await _db.CampaignClickLogs.AddAsync(new CampaignClickLog
                {
                    Id = Guid.NewGuid(),
                    CampaignSendLogId = p.cid,

                    // NOTE:
                    // If CampaignClickLog.CampaignId is NON-nullable in your DB/model, we must store Guid.Empty when unknown.
                    // If you later make it nullable, change this line to: CampaignId = campaignId
                    CampaignId = campaignId ?? Guid.Empty,

                    ContactId = contactId,
                    ButtonIndex = p.bi,
                    ButtonTitle = p.bt,
                    Destination = safeDest,
                    ClickedAt = now,
                    Ip = ip,
                    UserAgent = ua,
                    ClickType = clickType,
                    RunId = runId
                }, ct);

                await _db.SaveChangesAsync(ct);

                _log.LogInformation(
                    "CLICK WRITE-THROUGH cid={Cid} idx={Idx} type={Type} dest={Dest}",
                    p.cid, p.bi, clickType, safeDest);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Write-through insert failed. cid={Cid}", p.cid);
            }

            // 6) Enqueue for async worker (best effort)
            try
            {
                var enq = _queue.TryWrite(new ClickEvent(
                    CampaignSendLogId: p.cid,
                    ButtonIndex: p.bi,
                    ButtonTitle: p.bt,
                    Destination: safeDest,
                    ClickedAtUtc: now,
                    Ip: ip,
                    UserAgent: ua,
                    ClickType: clickType
                ));

                _log.LogInformation("CLICK ENQUEUE cid={Cid} idx={Idx} enqueued={Enqueued}", p.cid, p.bi, enq);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Queue write threw. cid={Cid}", p.cid);
            }

            // 7) First-click fast path (also stores ClickType) — best effort
            try
            {
                await _db.Database.ExecuteSqlRawAsync(
                    @"update ""CampaignSendLogs""
                         set ""IsClicked"" = TRUE,
                             ""ClickedAt"" = NOW() at time zone 'utc',
                             ""ClickType"" = {1}
                       where ""Id"" = {0}
                         and ""IsClicked"" = FALSE;",
                    parameters: new object[] { p.cid, clickType },
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "First-click update skipped.");
            }

            // 8) Redirect handling
            if (clickType is "call" or "whatsapp")
            {
                // Deep link → return an HTML/JS shim to trigger immediately, with a safe fallback link.
                var destHtml = HtmlEnc.Encode(safeDest);
                var destJs = JsEscape(safeDest);

                var html = $@"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta http-equiv=""x-ua-compatible"" content=""ie=edge"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <meta http-equiv=""refresh"" content=""0;url={destHtml}"">
  <title>Redirecting…</title>
  <style>
    body{{font-family:system-ui,-apple-system,Segoe UI,Roboto,Arial,sans-serif;padding:24px;}}
    a{{color:#2563eb;text-decoration:underline;}}
  </style>
  <script>
    window.addEventListener('load', function() {{
      try {{ window.location.replace('{destJs}'); }} catch (e) {{}}
      setTimeout(function() {{
        var f = document.getElementById('fallback');
        if (f) f.style.display = 'inline';
      }}, 1200);
    }});
  </script>
</head>
<body>
  <p>Redirecting… If you are not redirected automatically, <a id=""fallback"" style=""display:none"" href=""{destHtml}"">tap here</a>.</p>
</body>
</html>";

                Response.Headers["Cache-Control"] = "no-store, max-age=0";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                Response.Headers["Referrer-Policy"] = "no-referrer";
                Response.Headers["X-Frame-Options"] = "DENY";
                Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
                Response.Headers["Content-Security-Policy"] =
                    "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'";

                return new ContentResult
                {
                    Content = html,
                    ContentType = "text/html; charset=utf-8",
                    StatusCode = 200
                };
            }

            // Regular web links → normal 302

            Response.Headers["Cache-Control"] = "no-store, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            Response.Headers["X-Frame-Options"] = "DENY";
            Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            return Redirect(safeDest);
        }

        // --- helpers ---

        private static string ClassifyClickType(string normalizedDest, string scheme)
        {
            // scheme is pre-normalized by TryNormalizeAllowedDestination
            if (string.Equals(scheme, "tel", StringComparison.OrdinalIgnoreCase)) return "call";
            if (string.Equals(scheme, "wa", StringComparison.OrdinalIgnoreCase)) return "whatsapp";
            if (string.Equals(scheme, "whatsapp", StringComparison.OrdinalIgnoreCase)) return "whatsapp";

            // http/https → still treat WhatsApp hosts as whatsapp
            if (normalizedDest.StartsWith("https://wa.me/", StringComparison.OrdinalIgnoreCase)) return "whatsapp";
            if (normalizedDest.StartsWith("https://api.whatsapp.com/", StringComparison.OrdinalIgnoreCase)) return "whatsapp";

            return "web";
        }

        private static string JsEscape(string s) =>
            s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "");

        /// <summary>
        /// Accepts: http/https/tel/wa/whatsapp, plus shorthand wa.me/... and api.whatsapp.com/...
        /// Returns normalized absolute string and a normalized scheme hint ("http","https","tel","wa","whatsapp").
        /// </summary>
        private static bool TryNormalizeAllowedDestination(string? input, out string normalized, out string scheme)
        {
            normalized = string.Empty;
            scheme = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var cleaned = new string(input.Trim().Where(c => !char.IsControl(c)).ToArray());

            // Shorthand WhatsApp hosts without scheme → prefix https://
            if (!cleaned.Contains("://", StringComparison.Ordinal))
            {
                if (cleaned.StartsWith("wa.me/", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("api.whatsapp.com/", StringComparison.OrdinalIgnoreCase))
                {
                    var guess = "https://" + cleaned;
                    if (Uri.TryCreate(guess, UriKind.Absolute, out var waAbs))
                    {
                        normalized = waAbs.AbsoluteUri;
                        scheme = "https";
                        return true;
                    }
                }
            }

            // WhatsApp custom schemes (wa:, whatsapp:)
            if (cleaned.StartsWith("wa:", StringComparison.OrdinalIgnoreCase))
            {
                normalized = cleaned; scheme = "wa"; return true;
            }
            if (cleaned.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            {
                normalized = cleaned; scheme = "whatsapp"; return true;
            }

            // Absolute URIs
            if (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri))
            {
                // tel:
                if (uri.Scheme.Equals("tel", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = uri.ToString(); scheme = "tel"; return true;
                }

                // http/https (including WhatsApp hosts)
                if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = uri.AbsoluteUri;
                    scheme = uri.Scheme; // "http" or "https"
                    return true;
                }
            }

            return false;
        }
    }
}


