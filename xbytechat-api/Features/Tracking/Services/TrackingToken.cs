using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace xbytechat.api.Features.Tracking.Services
{
    public static class TrackingToken
    {
        public static string Create(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        public static T Decode<T>(string token)
        {
            var bytes = WebEncoders.Base64UrlDecode(token);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json)!;
        }
    }

    // What we put inside the token:
    public record ClickToken(Guid cid, int btnIndex, string btnTitle, string to, string phone);
}
