// 📄 Features/Entitlements/QuotaKeys.cs
namespace xbytechat.api.Features.Entitlements
{

    public static class QuotaKeys
    {
        // How many messages a business can send in a given period (usually Monthly)
        public const string MessagesPerMonth = "MESSAGES_PER_MONTH";

        public const string MessagesPerDay = "MESSAGES_PER_DAY";
        // How many campaigns can be sent per day
        public const string CampaignsPerDay = "CAMPAIGNS_PER_DAY";

        // How many templates can exist in total
        public const string TemplatesTotal = "TEMPLATES_TOTAL";
    }
}
