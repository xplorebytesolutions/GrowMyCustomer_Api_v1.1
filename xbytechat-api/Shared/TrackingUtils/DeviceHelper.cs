// File: Features/CTATracking/Utils/DeviceHelper.cs

namespace xbytechat.api.Shared.TrackingUtils
{
    public static class DeviceHelper
    {
        public static string GetDeviceType(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Unknown";

            userAgent = userAgent.ToLower();

            if (userAgent.Contains("mobile") || userAgent.Contains("android") || userAgent.Contains("iphone"))
                return "Mobile";

            if (userAgent.Contains("ipad") || userAgent.Contains("tablet"))
                return "Tablet";

            if (userAgent.Contains("windows") || userAgent.Contains("macintosh"))
                return "Desktop";

            return "Unknown";
        }
    }
}
