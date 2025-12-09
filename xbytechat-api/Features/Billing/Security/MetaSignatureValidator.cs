using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace xbytechat.api.Features.Billing.Security
{
    public interface IMetaSignatureValidator
    {
        bool IsValid(string signatureHeader, string payload);
    }

    public class MetaSignatureValidator : IMetaSignatureValidator
    {
        private readonly string _appSecret;

        public MetaSignatureValidator(IConfiguration config)
        {
            _appSecret = config["WhatsApp:MetaAppSecret"]
                         ?? throw new InvalidOperationException("WhatsApp:MetaAppSecret is not configured");
        }

        public bool IsValid(string signatureHeader, string payload)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrEmpty(payload))
                return false;

            // header format: sha256=HEX
            const string prefix = "sha256=";
            if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var sent = signatureHeader.Substring(prefix.Length);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            return CryptographicEquals(expected, sent);
        }

        private static bool CryptographicEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;

            var result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];

            return result == 0;
        }
    }
}
