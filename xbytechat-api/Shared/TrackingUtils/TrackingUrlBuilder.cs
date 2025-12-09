using System;
using System.Web;

namespace xbytechat.api.Shared.TrackingUtils
{
    public static class TrackingUrlBuilder
    {
        public static string BuildTrackingUrl(
            Guid businessId,
            string sourceType,
            Guid sourceId,
            string buttonText,
            string redirectUrl,
            Guid? messageId = null,
            Guid? contactId = null,
            string contactPhone = null,
            string sessionId = null,
            string threadId = null)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["src"] = sourceType;
            query["id"] = sourceId.ToString();
            query["btn"] = buttonText;
            query["to"] = redirectUrl;
            query["type"] = buttonText;
            if (messageId != null) query["msg"] = messageId.ToString();
            if (contactId != null) query["contact"] = contactId.ToString();
            if (!string.IsNullOrEmpty(contactPhone)) query["phone"] = contactPhone;
            if (!string.IsNullOrEmpty(sessionId)) query["session"] = sessionId;
            if (!string.IsNullOrEmpty(threadId)) query["thread"] = threadId;

            var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "https://yourdomain.com";
            return $"{baseUrl}/api/tracking/redirect?{query}";
        }
    }
}
