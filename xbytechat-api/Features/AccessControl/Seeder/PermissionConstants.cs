namespace xbytechat.api.Features.AccessControl.Seeder
{
    public static class PermissionConstants
    {
        public static class Dashboard
        {
            public const string View = "dashboard.view";
        }

        public static class Campaigns
        {
            public const string View = "campaign.view";
            public const string Create = "campaign.create";
            public const string Delete = "campaign.delete";
        }

        public static class Products
        {
            public const string View = "product.view";
            public const string Create = "product.create";
            public const string Delete = "product.delete";
        }

        public static class CRM
        {
            public const string ContactsView = "contacts.view";
            public const string TagsEdit = "tags.edit";
        }

        public static class Admin
        {
            public const string BusinessApprove = "admin.business.approve";
            public const string ViewLogs = "admin.logs.view";
        }

        // 🆕 Add more modules and permissions here as needed
    }
}
