// File: Features/CTATracking/Utils/GeoHelper.cs

using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace xbytechat.api.Shared.TrackingUtils
{
    public static class GeoHelper
    {
        public static async Task<string> GetCountryFromIP(string ipAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "::1")
                    return "Localhost";

                using var client = new HttpClient();
                var response = await client.GetStringAsync($"https://ipapi.co/{ipAddress}/json/");

                var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("country_name", out var countryProp))
                    return countryProp.GetString() ?? "Unknown";
            }
            catch
            {
                // fallback
            }

            return "Unknown";
        }
    }
}
