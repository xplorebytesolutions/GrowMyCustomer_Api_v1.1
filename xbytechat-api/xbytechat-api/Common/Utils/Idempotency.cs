using System;
using System.Security.Cryptography;
using System.Text;

namespace xbytechat.api.Common.Utils
{
    public static class Idempotency
    {
        /// <summary>
        /// SHA256 over a canonical string. Returns lowercase hex.
        /// </summary>
        public static string Sha256(string canonical)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(canonical ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
