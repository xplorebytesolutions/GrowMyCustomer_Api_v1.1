// 📄 Features/CampaignTracking/Services/ClickTokenService.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.CampaignTracking.Config;

namespace xbytechat.api.Features.CampaignTracking.Services
{
    public record ClickTokenPayload(
        Guid cid,            // CampaignSendLogId
        int bi,              // button index
        string bt,           // button title (optional)
        string to,           // destination absolute URL
        long iat,            // issued at (unix seconds)
        long exp             // expiry (unix seconds)
    );

    public interface IClickTokenService
    {
        string Create(ClickTokenPayload payload);
        bool TryValidate(string token, out ClickTokenPayload? payload, out string? error);
    }

    public class ClickTokenService : IClickTokenService
    {
        private readonly byte[] _key;

        public ClickTokenService(IOptions<TrackingOptions> opt)
        {
            _key = Encoding.UTF8.GetBytes(opt.Value.Secret ?? throw new ArgumentNullException(nameof(opt.Value.Secret)));
        }

        public string Create(ClickTokenPayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var data = Encoding.UTF8.GetBytes(json);
            var body = WebEncoders.Base64UrlEncode(data);

            var sig = ComputeHmac(body);
            return $"{body}.{sig}";
        }

        public bool TryValidate(string token, out ClickTokenPayload? payload, out string? error)
        {
            payload = null;
            error = null;

            var parts = token.Split('.');
            if (parts.Length != 2) { error = "format"; return false; }

            var body = parts[0];
            var sig = parts[1];

            var expected = ComputeHmac(body);
            // timing-safe compare
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sig), Encoding.UTF8.GetBytes(expected)))
            { error = "bad-signature"; return false; }

            try
            {
                var bytes = WebEncoders.Base64UrlDecode(body);
                var obj = JsonSerializer.Deserialize<ClickTokenPayload>(bytes);
                if (obj is null) { error = "decode"; return false; }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now > obj.exp) { error = "expired"; return false; }

                // minimal URL sanity check
                if (!Uri.TryCreate(obj.to, UriKind.Absolute, out var _)) { error = "bad-destination"; return false; }

                payload = obj;
                return true;
            }
            catch (Exception ex)
            {
                error = "exception:" + ex.GetType().Name;
                return false;
            }
        }

        private string ComputeHmac(string body)
        {
            using var h = new HMACSHA256(_key);
            var sig = h.ComputeHash(Encoding.UTF8.GetBytes(body));
            return WebEncoders.Base64UrlEncode(sig);
        }
    }
}
