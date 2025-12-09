namespace xbytechat.api.Features.AccessControl.Seeder
{
    public static class RolePermissionMapping
    {
        public static readonly Dictionary<string, List<string>> RolePermissions = new()
        {
            ["admin"] = new()
            {
                PermissionConstants.Dashboard.View,
                PermissionConstants.Campaigns.View,
                PermissionConstants.Campaigns.Create,
                PermissionConstants.Campaigns.Delete,
                PermissionConstants.Products.View,
                PermissionConstants.Products.Create,
                PermissionConstants.Products.Delete,
                PermissionConstants.CRM.ContactsView,
                PermissionConstants.CRM.TagsEdit,
                PermissionConstants.Admin.BusinessApprove,
                PermissionConstants.Admin.ViewLogs
            },

            ["business"] = new()
            {
                PermissionConstants.Dashboard.View,
                PermissionConstants.Campaigns.View,
                PermissionConstants.CRM.ContactsView,
                PermissionConstants.Products.View
            },

            ["staff"] = new()
            {
                PermissionConstants.Dashboard.View,
                PermissionConstants.CRM.ContactsView
            }
        };
    }
}
