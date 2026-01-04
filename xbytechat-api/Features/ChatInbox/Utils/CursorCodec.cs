using System;
using System.Text;
using System.Text.Json;

namespace xbytechat.api.Features.ChatInbox.Utils
{
    internal static class CursorCodec
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public static string Encode<T>(T obj)
        {
            var json = JsonSerializer.Serialize(obj, JsonOpts);
            var bytes = Encoding.UTF8.GetBytes(json);
            return Base64UrlEncode(bytes);
        }

        public static T? Decode<T>(string? cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor)) return default;

            try
            {
                var bytes = Base64UrlDecode(cursor.Trim());
                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<T>(json, JsonOpts);
            }
            catch
            {
                return default;
            }
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }
    }
}
